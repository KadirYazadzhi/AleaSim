using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using System.Text.Json;
using System.Collections.Concurrent;

namespace AleaSim.Domain.Services;

public class RouletteGameEngine : BaseGameEngine {
    private readonly int[] _wheel = Enumerable.Range(0, 37).ToArray(); // European Roulette (0-36)
    
    private readonly int[] _redNumbers = { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };
    
    private readonly ConcurrentDictionary<Guid, List<RouletteBet>> _pendingBets = new();
    private readonly ConcurrentDictionary<Guid, int> _roundCounters = new();

    public RouletteGameEngine(IRngService rngService, IRtpEngine rtpEngine, IJackpotService jackpotService) : base(rngService, rtpEngine, jackpotService) {
    }

    public record RouletteBet(string Type, string Value, decimal Amount);

    public override void PlaceBet(Guid sessionId, decimal amount, string betData) {
        base.PlaceBet(sessionId, amount, betData);
        
        var bets = JsonSerializer.Deserialize<List<RouletteBet>>(betData) ?? new();
        decimal totalAmount = bets.Sum(b => b.Amount);
        
        if (totalAmount != amount)
            throw new ArgumentException("Sum of bets does not match the total bet amount.");

        _pendingBets[sessionId] = bets;
    }

    public override GameRound ResolveRound(Guid sessionId) {
        if (!ActiveSessions.TryGetValue(sessionId, out var session))
            throw new InvalidOperationException("Session not found.");

        if (!_pendingBets.TryRemove(sessionId, out var bets))
            throw new InvalidOperationException("No bets placed for this round.");

        int roundNumber = _roundCounters.AddOrUpdate(sessionId, 1, (k, v) => v + 1);
        decimal totalBetAmount = bets.Sum(b => b.Amount);

        // Spin the wheel
        int winningNumber = RngService.GetNextInt(session.Seed, roundNumber, 0, 37);

        decimal totalWinAmount = 0;
        foreach (var bet in bets) {
            totalWinAmount += CalculateBetWin(bet, winningNumber);
        }

        // RTP Control
        if (totalWinAmount > 0 && !RtpEngine.IsOutcomeAllowed(session.GameId, session.UserId, totalWinAmount, totalBetAmount)) {
            totalWinAmount = 0; // Force loss if RTP is exceeded
        }

        if (totalWinAmount > 0) {
            UpdateBalance(session.UserId, totalWinAmount);
            RtpEngine.RecordWin(session.GameId, session.UserId, totalWinAmount);
        }

        return new GameRound {
            Id = Guid.NewGuid(),
            GameSessionId = sessionId,
            RoundNumber = roundNumber,
            InputData = JsonSerializer.Serialize(bets),
            RandomResult = JsonSerializer.Serialize(new { WinningNumber = winningNumber }),
            TotalBetAmount = totalBetAmount,
            TotalWinAmount = totalWinAmount,
            ExecutedAt = DateTime.UtcNow
        };
    }

    public override Outcome GetOutcome(Guid roundId) {
        return new Outcome { Id = Guid.NewGuid(), GameRoundId = roundId };
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
