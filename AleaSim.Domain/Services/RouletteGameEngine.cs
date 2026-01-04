using AleaSim.Shared.Models;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class RouletteGameEngine : BaseGameEngine {
    private Guid GameId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public RouletteGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope) 
        : base(rng, vault, brain, promo, jackpot, realTime, scope) {
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        return await ExecuteScopedAsync(async (repo, questService) => {
            var session = repo.GetSession(sessionId);
            var lastBet = repo.GetLastBet(sessionId);
            decimal betAmount = lastBet?.Amount ?? 1.0m;
            int nonce = repo.GetRoundCount(sessionId) + 1; // Restored
            
            // Deserialize Bets
            var bets = new List<RouletteBetDto>();
            try {
                if (!string.IsNullOrEmpty(lastBet?.BetData)) {
                    // Handle double serialization bug hack: Try deserialize once, if string, deserialize again
                    if (lastBet.BetData.StartsWith("\"")) {
                        var innerJson = JsonSerializer.Deserialize<string>(lastBet.BetData);
                        bets = JsonSerializer.Deserialize<List<RouletteBetDto>>(innerJson ?? "[]") ?? new();
                    } else {
                        bets = JsonSerializer.Deserialize<List<RouletteBetDto>>(lastBet.BetData) ?? new();
                    }
                }
            } catch { /* Ignore parsing errors, assume no bets */ }

            var decision = BrainService.DecideOutcome(session.UserId, GameId, betAmount, repo);
            
            // CMS: Find number matching decision
            int number = 0;
            decimal actualWin = 0;

            // Get all numbers 0-36
            var allNumbers = Enumerable.Range(0, 37).ToList();

            if (decision.TargetWinAmount > 0) {
                // Brain wants a WIN. Find numbers that pay > 0.
                var winningCandidates = allNumbers.Where(n => CalculatePayout(n, bets) > 0).ToList();

                if (winningCandidates.Any()) {
                    int idx = RngService.GetNextInt(session.Seed, nonce, 0, winningCandidates.Count);
                    number = winningCandidates[idx];
                } else {
                    // Impossible to win (user bet nothing?). Pick random.
                    number = RngService.GetNextInt(session.Seed, nonce, 0, 37);
                }
            } 
            else {
                // Brain wants a LOSS. Find numbers that pay 0.
                var losingCandidates = allNumbers.Where(n => CalculatePayout(n, bets) == 0).ToList();
                
                if (losingCandidates.Any()) {
                    int idx = RngService.GetNextInt(session.Seed, nonce, 0, losingCandidates.Count);
                    number = losingCandidates[idx];
                } else {
                    // Impossible to lose (user bet on everything?). Pick random.
                    number = RngService.GetNextInt(session.Seed, nonce, 0, 37);
                }
            }
            
            actualWin = CalculatePayout(number, bets);

            // Override Brain's target win with ACTUAL win from the physics
            VaultService.ProcessWin(session.UserId, actualWin, repo);
            BrainService.UpdateProfile(session.UserId, betAmount, actualWin);
            
            if (actualWin > 0) {
                questService.UpdateProgress(session.UserId, "WinAmount", (int)actualWin, repo, VaultService);
            }

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
