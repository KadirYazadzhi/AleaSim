using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using System.Collections.Concurrent;

namespace AleaSim.Domain.Services;

public class RtpEngine : IRtpEngine {
    private readonly ConcurrentDictionary<Guid, RTPStatistics> _gameStats = new();
    private readonly ConcurrentDictionary<Guid, RTPStatistics> _userStats = new();
    private readonly RTPStatistics _globalStats = new() { Id = Guid.NewGuid() };

    private const double MaxAllowedRtpDeviation = 0.05; // Allow 5% deviation from target

    public bool IsOutcomeAllowed(Guid gameId, Guid userId, decimal potentialWinAmount, decimal betAmount) {
        // Simple logic: check if the new RTP would be within acceptable bounds
        // In a real system, this would be more complex (e.g., using a pool or rolling window)
        
        var stats = _gameStats.GetOrAdd(gameId, id => new RTPStatistics { Id = Guid.NewGuid(), GameId = id });
        
        // This is a placeholder for actual game target RTP. 
        // In a real scenario, we would fetch this from the Game entity.
        double targetRtp = 0.95; 

        decimal projectedTotalPaid = stats.TotalPaid + potentialWinAmount;
        decimal projectedTotalWagered = stats.TotalWagered + betAmount;
        
        double projectedRtp = (double)(projectedTotalPaid / projectedTotalWagered);

        // Allow some variance, but if it's way above target, we might restrict it
        // Note: For very few rounds, RTP variance is expected.
        if (stats.TotalRounds > 1000 && projectedRtp > targetRtp + MaxAllowedRtpDeviation) {
            return false;
        }

        return true;
    }

    public void RecordBet(Guid gameId, Guid userId, decimal amount) {
        UpdateStats(gameId, userId, amount, 0);
    }

    public void RecordWin(Guid gameId, Guid userId, decimal amount) {
        UpdateStats(gameId, userId, 0, amount);
    }

    public RTPStatistics GetGameStats(Guid gameId) => _gameStats.GetOrAdd(gameId, id => new RTPStatistics { Id = Guid.NewGuid(), GameId = id });

    public RTPStatistics GetUserStats(Guid userId) => _userStats.GetOrAdd(userId, id => new RTPStatistics { Id = Guid.NewGuid(), UserId = id });

    private void UpdateStats(Guid gameId, Guid userId, decimal bet, decimal win) {
        var gStats = _gameStats.GetOrAdd(gameId, id => new RTPStatistics { Id = Guid.NewGuid(), GameId = id });
        var uStats = _userStats.GetOrAdd(userId, id => new RTPStatistics { Id = Guid.NewGuid(), UserId = id });

        lock (gStats) {
            gStats.TotalWagered += bet;
            gStats.TotalPaid += win;
            if (bet > 0) gStats.TotalRounds++;
            gStats.LastCalculated = DateTime.UtcNow;
        }

        lock (uStats) {
            uStats.TotalWagered += bet;
            uStats.TotalPaid += win;
            if (bet > 0) uStats.TotalRounds++;
            uStats.LastCalculated = DateTime.UtcNow;
        }

        lock (_globalStats) {
            _globalStats.TotalWagered += bet;
            _globalStats.TotalPaid += win;
            if (bet > 0) _globalStats.TotalRounds++;
            _globalStats.LastCalculated = DateTime.UtcNow;
        }
    }
}
