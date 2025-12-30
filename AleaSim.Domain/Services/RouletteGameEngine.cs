using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class RouletteGameEngine : BaseGameEngine {
    private readonly int[] _wheel = Enumerable.Range(0, 37).ToArray(); // European Roulette (0-36)
    
    private readonly int[] _redNumbers = { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };

    public RouletteGameEngine(IRngService rngService, IVaultService vaultService, IBrainService brainService, IPromotionService promotionService, IJackpotService jackpotService, IRealTimeService realTimeService, IServiceScopeFactory scopeFactory) 
        : base(rngService, vaultService, brainService, promotionService, jackpotService, realTimeService, scopeFactory) {
    }

    public record RouletteBet(string Type, string Value, decimal Amount);

    public override async Task PlaceBet(Guid sessionId, decimal amount, string betData) {
        var bets = JsonSerializer.Deserialize<List<RouletteBet>>(betData) ?? new();
        decimal totalAmount = bets.Sum(b => b.Amount);
        
        if (totalAmount != amount)
            throw new ArgumentException("Sum of bets does not match the total bet amount.");

        await base.PlaceBet(sessionId, amount, betData);
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        return await ExecuteScopedAsync(async repo => {
            using var transaction = repo.BeginTransaction();
            try {
                var session = repo.GetSession(sessionId);
                if (session == null) throw new InvalidOperationException("Session not found.");

                var lastBet = repo.GetLastBet(sessionId);
                if (lastBet == null) throw new InvalidOperationException("No bet placed for this round.");

                var bets = JsonSerializer.Deserialize<List<RouletteBet>>(lastBet.BetData) ?? new();
                
                int roundNumber = repo.GetRoundCount(sessionId) + 1;
                decimal totalBetAmount = lastBet.Amount;

                int winningNumber = -1;
                decimal totalWinAmount = 0;
                bool rtpAccepted = false;
                int attempts = 0;
                
                int maxAttempts = profile switch {
                    SpinProfile.HighVolatility => 1,
                    SpinProfile.LowVolatility => 10,
                    _ => 5
                };

                // Attempt to find a random result that satisfies Vault (Solvency)
                do {
                    attempts++;
                    winningNumber = RngService.GetNextInt(session.Seed, HashCode.Combine(roundNumber, attempts), 0, 37);
                    
                    totalWinAmount = 0;
                    foreach (var bet in bets) {
                        totalWinAmount += CalculateBetWin(bet, winningNumber);
                    }

                    if (totalWinAmount > 0) {
                        // Use VaultService to check solvency
                        if (VaultService.CanAffordWin(session.UserId, session.GameId, totalWinAmount, repo)) {
                            rtpAccepted = true;
                        }
                    } else {
                        rtpAccepted = true;
                    }
                } while (!rtpAccepted && attempts < maxAttempts);

                // Fallback: Find the result with the LOWEST payout if random attempts failed
                if (!rtpAccepted) {
                    decimal lowestWin = decimal.MaxValue;
                    int bestHouseNumber = 0;

                    for (int n = 0; n <= 36; n++) {
                        decimal currentWin = 0;
                        foreach (var bet in bets) {
                            currentWin += CalculateBetWin(bet, n);
                        }
                        
                        if (currentWin < lowestWin) {
                            lowestWin = currentWin;
                            bestHouseNumber = n;
                        }
                    }

                    winningNumber = bestHouseNumber;
                    totalWinAmount = lowestWin;
                }

                if (totalWinAmount > 0) {
                    // Credit win via Vault
                    VaultService.ProcessWin(session.UserId, totalWinAmount, repo);
                }
                
                // Update Brain Profile
                BrainService.UpdateProfile(session.UserId, totalBetAmount, totalWinAmount);
                
                // Track Tournament Win
                PromotionService.ProcessWinActivity(session.UserId, totalWinAmount, repo);

                var round = new GameRound {
                    Id = Guid.NewGuid(),
                    GameSessionId = sessionId,
                    RoundNumber = roundNumber,
                    InputData = lastBet.BetData,
                    RandomResult = JsonSerializer.Serialize(new { WinningNumber = winningNumber }),
                    TotalBetAmount = totalBetAmount,
                    TotalWinAmount = totalWinAmount,
                    ExecutedAt = DateTime.UtcNow
                };
                repo.SaveRound(round);

                // Link bet to round
                lastBet.GameRoundId = round.Id;
                repo.UpdateBet(lastBet);

                var outcome = new Outcome {
                    Id = Guid.NewGuid(),
                    GameRoundId = round.Id,
                    ResultJson = JsonSerializer.Serialize(new { WinningNumber = winningNumber, Win = totalWinAmount }),
                    WinAmount = totalWinAmount
                };
                repo.SaveOutcome(outcome);
                
                transaction.Commit();

                // Notify UI
                await RealTimeService.NotifyGameUpdate(session.UserId, new {
                    SessionId = sessionId,
                    Game = "Roulette",
                    WinningNumber = winningNumber,
                    Win = totalWinAmount
                });

                return round;
            }
            catch {
                transaction.Rollback();
                throw;
            }
        });
    }

    public override async Task<Outcome> GetOutcome(Guid roundId) {
        return await Task.Run(() => ExecuteScoped(repo => repo.GetOutcome(roundId)
               ?? new Outcome { Id = Guid.NewGuid(), GameRoundId = roundId }));
    }

    private decimal CalculateBetWin(RouletteBet bet, int winningNumber) {
        switch (bet.Type.ToLower()) {
            case "number":
                return int.Parse(bet.Value) == winningNumber ? bet.Amount * 36 : 0;
            case "color":
                bool isRed = _redNumbers.Contains(winningNumber);
                bool betRed = bet.Value.ToLower() == "red";
                if (winningNumber == 0) return 0;
                return isRed == betRed ? bet.Amount * 2 : 0;
            case "evenodd":
                if (winningNumber == 0) return 0;
                bool isEven = winningNumber % 2 == 0;
                bool betEven = bet.Value.ToLower() == "even";
                return isEven == betEven ? bet.Amount * 2 : 0;
            default:
                return 0;
        }
    }
}