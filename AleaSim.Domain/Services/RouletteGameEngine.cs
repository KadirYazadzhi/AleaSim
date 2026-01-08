using AleaSim.Shared.Models;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class RouletteGameEngine : BaseGameEngine {
    public class RouletteState { public int Nonce { get; set; } }
    private Guid GameId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public RouletteGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope) 
        : base(rng, vault, brain, promo, jackpot, realTime, scope) {   
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        return await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var session = repo.GetSession(sessionId);
            var lastBet = repo.GetLastBet(sessionId);
            decimal betAmount = lastBet?.Amount ?? 1.0m;
            var game = repo.GetGame(GameId);
            if (game != null && betAmount > (decimal)game.MaxBet) throw new Exception("Bet exceeds game maximum limit.");
            // Use Ticks to ensure uniqueness even if DB is lagging
            var state = string.IsNullOrEmpty(session.GameState) 
                ? new RouletteState() 
                : JsonSerializer.Deserialize<RouletteState>(session.GameState) ?? new RouletteState();
            int nonce = state.Nonce++;
            session.GameState = JsonSerializer.Serialize(state);
            
            var bets = new List<RouletteBetDto>();
            try {
                if (!string.IsNullOrEmpty(lastBet?.BetData)) {
                    if (lastBet.BetData.StartsWith("\"")) {
                        var innerJson = JsonSerializer.Deserialize<string>(lastBet.BetData);
                        bets = JsonSerializer.Deserialize<List<RouletteBetDto>>(innerJson ?? "[]") ?? new();
                    } else {
                        bets = JsonSerializer.Deserialize<List<RouletteBetDto>>(lastBet.BetData) ?? new();
                    }
                }
            } catch { }

            var decision = BrainService.DecideOutcome(session.UserId, GameId, betAmount, repo);
            
            int number = 0;
            var allNumbers = Enumerable.Range(0, 37).ToList();

            // FIXED LOGIC: Handle "Random" correctly
            if (decision.DecisionType == "Random") {
                // True RNG - Standard Casino Logic
                number = RngService.GetNextInt(session.Seed, nonce, 0, 37);
            }
            else if (decision.TargetWinAmount > 0) {
                // Force Win (Retention/Whale)
                var winningCandidates = allNumbers.Where(n => CalculatePayout(n, bets) > 0).ToList();
                if (winningCandidates.Any()) {
                    int idx = RngService.GetNextInt(session.Seed, nonce, 0, winningCandidates.Count);
                    number = winningCandidates[idx];
                } else {
                    number = RngService.GetNextInt(session.Seed, nonce, 0, 37); // Fallback
                }
            } 
            else {
                // Force Loss (Cooldown/Teaser)
                var losingCandidates = allNumbers.Where(n => CalculatePayout(n, bets) == 0).ToList();
                if (losingCandidates.Any()) {
                    int idx = RngService.GetNextInt(session.Seed, nonce, 0, losingCandidates.Count);
                    number = losingCandidates[idx];
                } else {
                    number = RngService.GetNextInt(session.Seed, nonce, 0, 37); // Fallback
                }
            }
            
            decimal actualWin = CalculatePayout(number, bets);

            // Important: Check if vault can pay.
            // For "Random" decisions, we bypass the strict Shadow Wallet check to allow luck.
            bool isRandom = decision.DecisionType == "Random";
            
            if (actualWin > 0 && !VaultService.CanAffordWin(session.UserId, GameId, actualWin, repo, strictShadowCheck: !isRandom)) {
                // Emergency Reroll to Loss
                var losingCandidates = allNumbers.Where(n => CalculatePayout(n, bets) == 0).ToList();
                if (losingCandidates.Any()) number = losingCandidates[RngService.GetNextInt(session.Seed, nonce+99, 0, losingCandidates.Count)];
                actualWin = CalculatePayout(number, bets);
            }

            if (actualWin > 0) {
                VaultService.ProcessWin(session.UserId, actualWin, repo);
                questService.UpdateProgress(session.UserId, "WinAmount", (int)actualWin, repo, VaultService);
            }
            
            BrainService.UpdateProfile(session.UserId, betAmount, actualWin);

            var round = new GameRound {
                Id = Guid.NewGuid(),
                GameSessionId = sessionId,
                TotalBetAmount = betAmount,
                TotalWinAmount = actualWin,
                RandomResult = JsonSerializer.Serialize(new { Number = number }),
                DecisionType = decision.DecisionType,
                ExecutedAt = DateTime.UtcNow
            };

            repo.SaveRound(round);
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Game = "Roulette", Number = number, Win = actualWin });
            return round;
        });
    }

    private decimal CalculatePayout(int number, List<RouletteBetDto> bets) {
        decimal total = 0;
        foreach (var bet in bets) {
            bool win = false;
            decimal mult = 0;

            if (bet.Type == "number" && int.TryParse(bet.Value, out int target) && target == number) {
                win = true; mult = 36m;
            }
            else if (bet.Type == "color") {
                bool isRed = new[] { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 }.Contains(number);
                if ((bet.Value == "red" && isRed) || (bet.Value == "black" && !isRed && number != 0)) {
                    win = true; mult = 2m;
                }
            }
            else if (bet.Type == "evenodd" && number != 0) {
                bool isEven = number % 2 == 0;
                if ((bet.Value == "even" && isEven) || (bet.Value == "odd" && !isEven)) {
                    win = true; mult = 2m;
                }
            }

            if (win) total += bet.Amount * mult;
        }
        return total;
    }

    public override Task ProcessAction(Guid sessionId, string action, string actionData) => Task.CompletedTask;
    public override Task<Outcome> GetOutcome(Guid roundId) => Task.FromResult(new Outcome());
    public override Task<object?> GetCurrentState(Guid sessionId) => Task.FromResult<object?>(null);
}