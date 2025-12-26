using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using System.Text.Json;

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
    
    public SlotGameEngine(IRngService rngService, IRtpEngine rtpEngine, IJackpotService jackpotService, IGameRepository repository) 
        : base(rngService, rtpEngine, jackpotService, repository) {
    }

    public override GameRound ResolveRound(Guid sessionId) {
        var session = Repository.GetSession(sessionId);
        if (session == null) throw new InvalidOperationException("Session not found.");

        var lastBet = Repository.GetLastBet(sessionId);
        if (lastBet == null) throw new InvalidOperationException("No bet found.");

        int roundNumber = Repository.GetRoundCount(sessionId) + 1;

        // Calculate stop positions for 3 reels
        int[] stops = new int[3];
        for (int i = 0; i < 3; i++) {
            stops[i] = RngService.GetNextInt(session.Seed, HashCode.Combine(roundNumber, i), 0, _reelStrips[i].Length);
        }

        int[] resultSymbols = new int[3];
        for (int i = 0; i < 3; i++) {
            resultSymbols[i] = _reelStrips[i][stops[i]];
        }

        decimal winAmount = CalculateWin(resultSymbols, lastBet.Amount);

        // Jackpot check
        if (JackpotService.CheckJackpotTrigger(session.GameId, session.Seed, roundNumber, out decimal jackpotWin)) {
            winAmount += jackpotWin;
        }

        // Check RTP Engine
        if (winAmount > 0 && !RtpEngine.IsOutcomeAllowed(session.GameId, session.UserId, winAmount, lastBet.Amount)) {
            winAmount = 0;
        }

        if (winAmount > 0) {
            Repository.UpdateUserBalance(session.UserId, winAmount);
            RtpEngine.RecordWin(session.GameId, session.UserId, winAmount);
        }

        var round = new GameRound {
            Id = Guid.NewGuid(),
            GameSessionId = sessionId,
            RoundNumber = roundNumber,
            InputData = JsonSerializer.Serialize(new { Stops = stops }),
            RandomResult = JsonSerializer.Serialize(new { Symbols = resultSymbols }),
            TotalBetAmount = lastBet.Amount,
            TotalWinAmount = winAmount,
            ExecutedAt = DateTime.UtcNow
        };

        Repository.SaveRound(round);

        // Link bet to round
        lastBet.GameRoundId = round.Id;
        Repository.UpdateBet(lastBet);
        
        var outcome = new Outcome {
            Id = Guid.NewGuid(),
            GameRoundId = round.Id,
            ResultJson = JsonSerializer.Serialize(new { Symbols = resultSymbols, Win = winAmount }),
            WinAmount = winAmount
        };
        Repository.SaveOutcome(outcome);
        
        return round;
    }

    public override Outcome GetOutcome(Guid roundId) {
        return Repository.GetOutcome(roundId) 
               ?? new Outcome { Id = Guid.NewGuid(), GameRoundId = roundId, ResultJson = "{}" };
    }

    private decimal CalculateWin(int[] symbols, decimal betAmount) {
        if (symbols[0] == symbols[1] && symbols[1] == symbols[2]) {
            if (_paytable.TryGetValue(symbols[0], out decimal multiplier)) {
                return betAmount * multiplier;
            }
        }
        return 0;
    }
}
