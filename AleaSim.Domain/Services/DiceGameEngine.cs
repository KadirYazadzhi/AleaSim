using AleaSim.Domain.Entities;
using AleaSim.Domain.Enums;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models;
using AleaSim.Shared.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class DiceGameEngine : BaseGameEngine {
    private readonly Guid _gameId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly IRedisCacheService _cache;

    public DiceGameEngine(
        IRngService rng,
        IVaultService vault,
        IBrainService brain,
        IPromotionService promo,
        IJackpotService jackpot,
        IRealTimeService realTime,
        IServiceScopeFactory scopeFactory,
        ILockService lockService,
        IRedisCacheService cache) : base(rng, vault, brain, promo, jackpot, realTime, scopeFactory, lockService) {
        _cache = cache;
    }

    public override async Task PlaceBet(Guid userId, Guid sessionId, decimal amount, string? betData) {
        try {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto = JsonSerializer.Deserialize<DiceBetDto>(betData ?? "{}", options);

            if (dto == null) throw new Exception("Invalid bet data.");

            if (dto.Mode.Equals("Slider", StringComparison.OrdinalIgnoreCase)) {
                if (dto.TargetValue < 2 || dto.TargetValue > 98) 
                    throw new Exception("Slider target value must be between 2 and 98.");
                if (!dto.Condition.Equals("Over", StringComparison.OrdinalIgnoreCase) && 
                    !dto.Condition.Equals("Under", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Slider condition must be Over or Under.");
            } else if (dto.Mode.Equals("Multi", StringComparison.OrdinalIgnoreCase)) {
                if (dto.MultiDiceSelected == null || !dto.MultiDiceSelected.Any())
                    throw new Exception("Multi mode requires at least one selected number.");
                if (dto.MultiDiceSelected.Any(n => n < 1 || n > 6))
                    throw new Exception("Multi mode numbers must be between 1 and 6.");
            } else {
                throw new Exception("Invalid dice mode.");
            }
        } catch (Exception ex) when (ex is not Exception) { 
             throw new Exception("Security Alert: Bet data tampering detected.");
        }

        await base.PlaceBet(userId, sessionId, amount, betData);
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        using var lockHandle = await LockService.AcquireLockAsync(sessionId.ToString(), TimeSpan.FromSeconds(5));

        var round = await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var session = repo.GetSession(sessionId);
            if (session == null) throw new Exception("Session not found");
            var lastBet = repo.GetLastBet(sessionId);
            if (lastBet == null) throw new Exception("No bet found");

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var betData = JsonSerializer.Deserialize<DiceBetDto>(lastBet.BetData, options) ?? new DiceBetDto();
            int roundNum = repo.GetRoundCount(sessionId) + 1;
            int nonce = roundNum;

            var directive = await BrainService.DecideOutcomeAsync(session.UserId, _gameId, lastBet.Amount, repo);
            
            DiceResultDto result = new DiceResultDto();
            
            if (betData.Mode.Equals("Slider", StringComparison.OrdinalIgnoreCase)) {
                result = ResolveSliderDice(session.ServerSeed, session.ClientSeed, nonce, betData, directive);
            } else {
                result = ResolveMultiDice(session.ServerSeed, session.ClientSeed, nonce, betData, directive);
            }

            decimal winAmount = lastBet.Amount * result.PayoutMultiplier;

            // Strict pRTP check
            if (winAmount > 0 && !await VaultService.CanAffordWinAsync(session.UserId, _gameId, winAmount, repo, strictShadowCheck: directive.DecisionType != "Random")) {
                // Force loss if can't afford
                result = ForceDiceLoss(session.ServerSeed, session.ClientSeed, nonce, betData);
                winAmount = 0;
            }

            var roundId = Guid.NewGuid();

            if (winAmount > 0) {
                repo.UpdateGamePoolBalance(_gameId, -winAmount);
                await VaultService.ProcessWinAsync(session.UserId, winAmount, repo, roundId);
                await questService.UpdateProgressAsync(session.UserId, "WinAmount", winAmount, repo, RealTimeService, VaultService);
                
                session.TotalWon += winAmount;
            }
            
            await BrainService.UpdateProfileAsync(session.UserId, lastBet.Amount, winAmount, repo);

            int roundCount = repo.GetRoundCount(sessionId);
            RotateServerSeed(session, roundCount);

            var shadowDirective = await BrainService.DecideOutcomeAsync(session.UserId, session.GameId, lastBet.Amount, repo, isShadowMode: true);

            var round = new GameRound {
                Id = roundId,
                GameSessionId = sessionId,
                RoundNumber = roundNum,
                TotalBetAmount = lastBet.Amount,
                TotalWinAmount = winAmount,
                ExecutedAt = DateTime.UtcNow,
                ShadowBrainResult = JsonSerializer.Serialize(shadowDirective),
                RandomResult = JsonSerializer.Serialize(result),
                DecisionType = directive.DecisionType,
                ServerSeed = session.ServerSeed ?? "",
                ServerSeedHash = session.ServerSeedHash ?? "",
                ClientSeed = session.ClientSeed ?? "",
                Nonce = nonce
            };

            repo.SaveRound(round);
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Game = "Dice", Result = result });
            return round;
        });

        // Sync Cache AFTER Transaction Commit
        using (var scope = ScopeFactory.CreateScope()) {
            var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
            var session = repo.GetSession(sessionId);
            if (session != null) await BrainService.SyncProfileToCacheAsync(session.UserId, repo).ConfigureAwait(false);
        }

        return round;
    }

    private DiceResultDto ResolveSliderDice(string serverSeed, string clientSeed, int nonce, DiceBetDto bet, BrainDirective directive) {
        decimal winChance = bet.Condition.Equals("Over", StringComparison.OrdinalIgnoreCase) ? (100 - bet.TargetValue) : bet.TargetValue;
        if (winChance <= 0 || winChance >= 100) winChance = 50;

        // House edge 1%
        decimal multiplier = 99m / winChance;
        
        decimal roll = 0;
        bool isWin = false;

        if (directive.DecisionType.Equals("Random", StringComparison.OrdinalIgnoreCase)) {
            roll = (decimal)(RngService.GetNextDouble(serverSeed, clientSeed, nonce) * 100);
        } else if (directive.TargetWinAmount > 0) {
            // Force Win
            if (bet.Condition.Equals("Over", StringComparison.OrdinalIgnoreCase)) roll = bet.TargetValue + 0.01m + (decimal)RngService.GetNextDouble(serverSeed, clientSeed, nonce) * (100 - bet.TargetValue - 0.01m);
            else roll = (decimal)RngService.GetNextDouble(serverSeed, clientSeed, nonce) * (bet.TargetValue - 0.01m);
        } else {
            // Force Loss
            if (bet.Condition.Equals("Over", StringComparison.OrdinalIgnoreCase)) roll = (decimal)RngService.GetNextDouble(serverSeed, clientSeed, nonce) * bet.TargetValue;
            else roll = bet.TargetValue + (decimal)RngService.GetNextDouble(serverSeed, clientSeed, nonce) * (100 - bet.TargetValue);
        }

        // Clamp
        roll = Math.Round(roll, 2);
        if (roll < 0) roll = 0;
        if (roll > 100) roll = 100;

        if (bet.Condition.Equals("Over", StringComparison.OrdinalIgnoreCase)) isWin = roll > bet.TargetValue;
        else isWin = roll < bet.TargetValue;

        return new DiceResultDto {
            ResultValue = roll,
            IsWin = isWin,
            PayoutMultiplier = isWin ? multiplier : 0
        };
    }

    private DiceResultDto ResolveMultiDice(string serverSeed, string clientSeed, int nonce, DiceBetDto bet, BrainDirective directive) {
        var selected = bet.MultiDiceSelected ?? new List<int> { 6 };
        
        if (directive.DecisionType == "Random") {
            var dice = new List<int>();
            for (int i = 0; i < 10; i++) dice.Add(RngService.GetNextInt(serverSeed, clientSeed, nonce + i, 1, 7));
            return CalculateMultiResult(dice, selected);
        } else if (directive.TargetWinAmount > 0) {
            // Force a win (at least 3-6 hits for profit)
            int targetHits = RngService.GetNextInt(serverSeed, clientSeed, nonce, 3, 7);
            var dice = GenerateDiceWithHits(serverSeed, clientSeed, nonce, selected, targetHits);
            return CalculateMultiResult(dice, selected);
        } else {
            // Force a loss (0-2 hits for visual variety)
            int targetHits = RngService.GetNextInt(serverSeed, clientSeed, nonce, 0, 3);
            var dice = GenerateDiceWithHits(serverSeed, clientSeed, nonce, selected, targetHits);
            return CalculateMultiResult(dice, selected);
        }
    }

    private List<int> GenerateDiceWithHits(string serverSeed, string clientSeed, int nonce, List<int> selected, int targetHits) {
        var dice = new List<int>();
        var notSelected = Enumerable.Range(1, 6).Where(n => !selected.Contains(n)).ToList();

        // 1. Fill with the required number of hits
        for (int i = 0; i < targetHits; i++) {
            int valIdx = RngService.GetNextInt(serverSeed, clientSeed, nonce + i, 0, selected.Count);
            dice.Add(selected[valIdx]);
        }

        // 2. Fill the rest with non-hits (or random if all are selected, which shouldn't happen with UI limits)
        for (int i = targetHits; i < 10; i++) {
            if (notSelected.Any()) {
                int valIdx = RngService.GetNextInt(serverSeed, clientSeed, nonce + i, 0, notSelected.Count);
                dice.Add(notSelected[valIdx]);
            } else {
                dice.Add(RngService.GetNextInt(serverSeed, clientSeed, nonce + i, 1, 7));
            }
        }

        // 3. Robust Shuffle using Fisher-Yates with seeds for provable fairness
        var result = dice.ToList();
        for (int i = result.Count - 1; i > 0; i--) {
            // Use a specific salt for shuffling to ensure it's different from value generation
            int j = RngService.GetNextInt(serverSeed, clientSeed, nonce + 50 + i, 0, i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        
        return result;
    }

    private DiceResultDto CalculateMultiResult(List<int> dice, List<int> selected) {
        int hits = dice.Count(d => selected.Contains(d));
        
        decimal riskFactor = selected.Count switch {
            1 => 5.0m, 2 => 2.5m, 3 => 1.5m, 4 => 1.0m, 5 => 0.8m, 6 => 0.1m, _ => 1.0m
        };

        decimal baseMultiplier = hits switch {
            >= 10 => 100, 9 => 50, 8 => 20, 7 => 10, 6 => 5, 5 => 3, 4 => 2, 3 => 1.2m, 2 => 1.0m, 1 => 0.5m, _ => 0
        };

        decimal finalMultiplier = Math.Round(baseMultiplier * riskFactor, 2);

        return new DiceResultDto {
            MultiDiceResults = dice,
            IsWin = finalMultiplier >= 1.0m,
            PayoutMultiplier = finalMultiplier
        };
    }

    private DiceResultDto ForceDiceLoss(string serverSeed, string clientSeed, int nonce, DiceBetDto bet) {
        if (bet.Mode.Equals("Slider", StringComparison.OrdinalIgnoreCase)) {
            decimal roll = bet.Condition.Equals("Over", StringComparison.OrdinalIgnoreCase) ? bet.TargetValue - 1 : bet.TargetValue + 1;
            if (roll < 0) roll = 0; if (roll > 100) roll = 100;
            return new DiceResultDto { ResultValue = roll, IsWin = false, PayoutMultiplier = 0 };
        } else {
            // Use the same smart generation even for forced losses to avoid "all 1s"
            var selected = bet.MultiDiceSelected ?? new List<int> { 6 };
            int targetHits = RngService.GetNextInt(serverSeed, clientSeed, nonce, 0, 2); // Force clear loss (0 or 1 hits)
            var dice = GenerateDiceWithHits(serverSeed, clientSeed, nonce, selected, targetHits);
            return CalculateMultiResult(dice, selected);
        }
    }

    public override Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData) => Task.CompletedTask;
    public override Task<Outcome> GetOutcome(Guid roundId) => Task.FromResult(new Outcome { GameRoundId = roundId });
    public override Task<object?> GetCurrentState(Guid sessionId) => Task.FromResult<object?>(null);
}
