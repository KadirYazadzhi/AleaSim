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
    private readonly IMemoryCache _cache;

    public DiceGameEngine(
        IRngService rng,
        IVaultService vault,
        IBrainService brain,
        IPromotionService promo,
        IJackpotService jackpot,
        IRealTimeService realTime,
        IServiceScopeFactory scopeFactory,
        ILockService lockService,
        IMemoryCache cache) : base(rng, vault, brain, promo, jackpot, realTime, scopeFactory, lockService) {
        _cache = cache;
    }

    public override async Task PlaceBet(Guid userId, Guid sessionId, decimal amount, string betData) {
        try {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto = JsonSerializer.Deserialize<DiceBetDto>(betData, options);

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

        return await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var session = repo.GetSession(sessionId);
            if (session == null) throw new Exception("Session not found");
            var lastBet = repo.GetLastBet(sessionId);
            if (lastBet == null) throw new Exception("No bet found");

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var betData = JsonSerializer.Deserialize<DiceBetDto>(lastBet.BetData, options) ?? new DiceBetDto();
            int roundNum = repo.GetRoundCount(sessionId) + 1;
            int nonce = roundNum;

            var directive = BrainService.DecideOutcome(session.UserId, _gameId, lastBet.Amount, repo);
            
            DiceResultDto result = new DiceResultDto();
            
            if (betData.Mode.Equals("Slider", StringComparison.OrdinalIgnoreCase)) {
                result = ResolveSliderDice(session.Seed, nonce, betData, directive);
            } else {
                result = ResolveMultiDice(session.Seed, nonce, betData, directive);
            }

            decimal winAmount = lastBet.Amount * result.PayoutMultiplier;

            // Strict pRTP check
            if (winAmount > 0 && !await VaultService.CanAffordWinAsync(session.UserId, _gameId, winAmount, repo, strictShadowCheck: directive.DecisionType != "Random")) {
                // Force loss if can't afford
                result = ForceDiceLoss(session.Seed, nonce, betData);
                winAmount = 0;
            }

            if (winAmount > 0) {
                repo.UpdateGamePoolBalance(_gameId, -winAmount);
                await VaultService.ProcessWinAsync(session.UserId, winAmount, repo);
                await questService.UpdateProgressAsync(session.UserId, "WinAmount", winAmount, repo, RealTimeService, VaultService);
            }
            
            await questService.UpdateProgressAsync(session.UserId, "SpinCount", 1, repo, RealTimeService, VaultService);
            BrainService.UpdateProfile(session.UserId, lastBet.Amount, winAmount, repo);

            var round = new GameRound {
                Id = Guid.NewGuid(),
                GameSessionId = sessionId,
                RoundNumber = roundNum,
                TotalBetAmount = lastBet.Amount,
                TotalWinAmount = winAmount,
                ExecutedAt = DateTime.UtcNow,
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
    }

    private DiceResultDto ResolveSliderDice(int seed, int nonce, DiceBetDto bet, BrainDirective directive) {
        decimal winChance = bet.Condition.Equals("Over", StringComparison.OrdinalIgnoreCase) ? (100 - bet.TargetValue) : bet.TargetValue;
        if (winChance <= 0 || winChance >= 100) winChance = 50;

        // House edge 1%
        decimal multiplier = 99m / winChance;
        
        decimal roll = 0;
        bool isWin = false;

        if (directive.DecisionType.Equals("Random", StringComparison.OrdinalIgnoreCase)) {
            roll = (decimal)(RngService.GetNextDouble(seed, nonce) * 100);
        } else if (directive.TargetWinAmount > 0) {
            // Force Win
            if (bet.Condition.Equals("Over", StringComparison.OrdinalIgnoreCase)) roll = bet.TargetValue + 0.01m + (decimal)RngService.GetNextDouble(seed, nonce) * (100 - bet.TargetValue - 0.01m);
            else roll = (decimal)RngService.GetNextDouble(seed, nonce) * (bet.TargetValue - 0.01m);
        } else {
            // Force Loss
            if (bet.Condition.Equals("Over", StringComparison.OrdinalIgnoreCase)) roll = (decimal)RngService.GetNextDouble(seed, nonce) * bet.TargetValue;
            else roll = bet.TargetValue + (decimal)RngService.GetNextDouble(seed, nonce) * (100 - bet.TargetValue);
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

    private DiceResultDto ResolveMultiDice(int seed, int nonce, DiceBetDto bet, BrainDirective directive) {
        var dice = new List<int>();
        var selected = bet.MultiDiceSelected ?? new List<int> { 6 };
        
        // Simulating 10 dice
        for (int i = 0; i < 10; i++) {
            dice.Add(RngService.GetNextInt(seed, nonce + i, 1, 7));
        }

        int hits = dice.Count(d => selected.Contains(d));
        
        // Multi-dice balanced math for 10 dice:
        // User selects target numbers. Hits are counted.
        // We need frequent wins to keep user engaged.
        
        decimal multiplier = hits switch {
            >= 10 => 500,
            9 => 100,
            8 => 50,
            7 => 20,
            6 => 10,
            5 => 5,
            4 => 2,
            3 => 1.5m,
            2 => 1.1m, // Small profit
            1 => 0.5m, // Half back (keeps them playing)
            _ => 0
        };

        if (directive.DecisionType != "Random") {
            // If Brain wants a win/loss, we might adjust hits, 
            // but for now, we'll let Multi-Dice be purely RNG-heavy 
            // unless we want to implement complex symbol-swapping.
        }

        return new DiceResultDto {
            MultiDiceResults = dice,
            IsWin = multiplier > 0,
            PayoutMultiplier = multiplier
        };
    }

    private DiceResultDto ForceDiceLoss(int seed, int nonce, DiceBetDto bet) {
        if (bet.Mode.Equals("Slider", StringComparison.OrdinalIgnoreCase)) {
            decimal roll = bet.Condition.Equals("Over", StringComparison.OrdinalIgnoreCase) ? bet.TargetValue - 1 : bet.TargetValue + 1;
            if (roll < 0) roll = 0; if (roll > 100) roll = 100;
            return new DiceResultDto { ResultValue = roll, IsWin = false, PayoutMultiplier = 0 };
        } else {
            return new DiceResultDto { MultiDiceResults = new List<int> { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }, IsWin = false, PayoutMultiplier = 0 };
        }
    }

    public override Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData) => Task.CompletedTask;
    public override Task<Outcome> GetOutcome(Guid roundId) => Task.FromResult(new Outcome { GameRoundId = roundId });
    public override Task<object?> GetCurrentState(Guid sessionId) => Task.FromResult<object?>(null);
}
