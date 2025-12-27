using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class RouletteGameEngine : BaseGameEngine {
    private readonly int[] _wheel = Enumerable.Range(0, 37).ToArray(); // European Roulette (0-36)
    
    private readonly int[] _redNumbers = { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };

    public RouletteGameEngine(IRngService rngService, IRtpEngine rtpEngine, IJackpotService jackpotService, IRealTimeService realTimeService, IServiceScopeFactory scopeFactory) 
        : base(rngService, rtpEngine, jackpotService, realTimeService, scopeFactory) {
    }

    public record RouletteBet(string Type, string Value, decimal Amount);

    public override void PlaceBet(Guid sessionId, decimal amount, string betData) {
        var bets = JsonSerializer.Deserialize<List<RouletteBet>>(betData) ?? new();
        decimal totalAmount = bets.Sum(b => b.Amount);
        
        if (totalAmount != amount)
            throw new ArgumentException("Sum of bets does not match the total bet amount.");

        base.PlaceBet(sessionId, amount, betData);
    }

    public override GameRound ResolveRound(Guid sessionId) {
        return ExecuteScoped(repo => {
            using var transaction = repo.BeginTransaction();
            try {
                var session = repo.GetSession(sessionId);
                if (session == null) throw new InvalidOperationException("Session not found.");

                var lastBet = repo.GetLastBet(sessionId);
                if (lastBet == null) throw new InvalidOperationException("No bet placed for this round.");

                var bets = JsonSerializer.Deserialize<List<RouletteBet>>(lastBet.BetData) ?? new();
                
                int roundNumber = repo.GetRoundCount(sessionId) + 1;
                decimal totalBetAmount = lastBet.Amount;

                // Spin the wheel
                int winningNumber = RngService.GetNextInt(session.Seed, roundNumber, 0, 37);

                decimal totalWinAmount = 0;
                foreach (var bet in bets) {
                    totalWinAmount += CalculateBetWin(bet, winningNumber);
                }

                // RTP Control
                if (totalWinAmount > 0) {
                     if (!RtpEngine.ProcessWin(session.GameId, session.UserId, totalWinAmount, totalBetAmount, repo)) {
                        totalWinAmount = 0; // Force loss if RTP is exceeded
                     } else {
                        repo.UpdateUserBalance(session.UserId, totalWinAmount);
                     }
                }

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
                RealTimeService.NotifyGameUpdate(session.UserId, new {
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

    public override Outcome GetOutcome(Guid roundId) {
        return ExecuteScoped(repo => repo.GetOutcome(roundId)
               ?? new Outcome { Id = Guid.NewGuid(), GameRoundId = roundId });
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