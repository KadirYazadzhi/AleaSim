using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using System.Text.Json;
using System.Collections.Concurrent;

namespace AleaSim.Domain.Services;

public class SlotGameEngine : BaseGameEngine {
    private readonly int[][] _reelStrips = new[] {
        new[] { 1, 2, 3, 4, 5, 1, 2, 3 }, // Reel 1
        new[] { 1, 2, 3, 4, 5, 1, 2, 3 }, // Reel 2
        new[] { 1, 2, 3, 4, 5, 1, 2, 3 }  // Reel 3
    };

    private readonly Dictionary<int, decimal> _paytable = new() {
        { 1, 10m }, // 3 of symbol 1 pays 10x
        { 2, 5m },  // 3 of symbol 2 pays 5x
        { 3, 2m },  // 3 of symbol 3 pays 2x
        { 4, 1m },  // 3 of symbol 4 pays 1x
        { 5, 0.5m } // 3 of symbol 5 pays 0.5x
    };

    private readonly ConcurrentDictionary<Guid, decimal> _currentBets = new();
    private readonly ConcurrentDictionary<Guid, int> _roundCounters = new();

    public SlotGameEngine(IRngService rngService, IRtpEngine rtpEngine, IJackpotService jackpotService) : base(rngService, rtpEngine, jackpotService) {
    }

    public override void PlaceBet(Guid sessionId, decimal amount, string betData) {
        base.PlaceBet(sessionId, amount, betData);
        _currentBets[sessionId] = amount;
    }

    public override GameRound ResolveRound(Guid sessionId) {
        if (!ActiveSessions.TryGetValue(sessionId, out var session))
            throw new InvalidOperationException("Session not found.");

        if (!_currentBets.TryRemove(sessionId, out decimal betAmount))
            throw new InvalidOperationException("No bet placed for this round.");

        int roundNumber = _roundCounters.AddOrUpdate(sessionId, 1, (k, v) => v + 1);

        // Calculate stop positions for 3 reels
        int[] stops = new int[3];
        for (int i = 0; i < 3; i++) {
            stops[i] = RngService.GetNextInt(session.Seed, HashCode.Combine(roundNumber, i), 0, _reelStrips[i].Length);
        }

        int[] resultSymbols = new int[3];
        for (int i = 0; i < 3; i++) {
            resultSymbols[i] = _reelStrips[i][stops[i]];
        }

        decimal winAmount = CalculateWin(resultSymbols, betAmount);

        // Jackpot check
        if (JackpotService.CheckJackpotTrigger(session.GameId, session.Seed, roundNumber, out decimal jackpotWin)) {
            winAmount += jackpotWin;
        }

        // Check RTP Engine
        if (winAmount > 0 && !RtpEngine.IsOutcomeAllowed(session.GameId, session.UserId, winAmount, betAmount)) {
            // If not allowed, we "force" a loss for this simulation
            // In a real system, we might retry or pick a different outcome
            winAmount = 0;
        }

        if (winAmount > 0) {
            UpdateBalance(session.UserId, winAmount);
            RtpEngine.RecordWin(session.GameId, session.UserId, winAmount);
        }

        var round = new GameRound {
            Id = Guid.NewGuid(),
            GameSessionId = sessionId,
            RoundNumber = roundNumber,
            InputData = JsonSerializer.Serialize(new { Stops = stops }),
            RandomResult = JsonSerializer.Serialize(new { Symbols = resultSymbols }),
            TotalBetAmount = betAmount,
            TotalWinAmount = winAmount,
            ExecutedAt = DateTime.UtcNow
        };

        return round;
    }

    public override Outcome GetOutcome(Guid roundId) {
        // This would typically fetch from a repository. 
        // For now, we return a mock or expect the caller to have the round.
        return new Outcome {
            Id = Guid.NewGuid(),
            GameRoundId = roundId,
            ResultJson = "{}", // To be filled by caller or stored
            WinAmount = 0
        };
    }

    private decimal CalculateWin(int[] symbols, decimal betAmount) {
        // Simple 3-reel match logic
        if (symbols[0] == symbols[1] && symbols[1] == symbols[2]) {
            if (_paytable.TryGetValue(symbols[0], out decimal multiplier)) {
                return betAmount * multiplier;
            }
        }
        return 0;
    }
}
