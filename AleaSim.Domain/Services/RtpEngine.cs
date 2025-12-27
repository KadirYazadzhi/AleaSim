using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class RtpEngine : IRtpEngine {
    private const double MaxAllowedRtpDeviation = 0.05; 
    private readonly object _lock = new();
    private readonly IRealTimeService _realTimeService; // Added

    public RtpEngine(IRealTimeService realTimeService) {
        _realTimeService = realTimeService;
    }

    public bool ProcessWin(Guid gameId, Guid userId, decimal winAmount, decimal betAmount, IGameRepository repo) {
        lock (_lock) {
            var stats = repo.GetOrCreateGameStats(gameId);
            
            double targetRtp = 0.95; 

            decimal projectedTotalPaid = stats.TotalPaid + winAmount;
            decimal projectedTotalWagered = stats.TotalWagered + betAmount; // Note: Wagered usually added in RecordBet, but we check projection
            
            // Re-calculate based on actual stored totals (Wagered was likely already added by RecordBet)
            projectedTotalWagered = stats.TotalWagered; 
            if (projectedTotalWagered == 0) projectedTotalWagered = betAmount; // Safety net

            double projectedRtp = (double)(projectedTotalPaid / projectedTotalWagered);

            if (stats.TotalRounds > 1000 && projectedRtp > targetRtp + MaxAllowedRtpDeviation) {
                return false;
            }

            // If allowed, record it immediately to prevent race conditions
            repo.UpdateRtpStats(gameId, userId, 0, winAmount);
            
            // Notify
             if (stats.TotalWagered > 0)
                _realTimeService.NotifyRtpUpdate(gameId, (double)((stats.TotalPaid + winAmount) / stats.TotalWagered));

            return true;
        }
    }

    public void RecordBet(Guid gameId, Guid userId, decimal amount, IGameRepository repo) {
        lock (_lock) {
            repo.UpdateRtpStats(gameId, userId, amount, 0);
            var stats = repo.GetOrCreateGameStats(gameId);
            if (stats.TotalWagered > 0)
                _realTimeService.NotifyRtpUpdate(gameId, (double)(stats.TotalPaid / stats.TotalWagered));
        }
    }

    public RTPStatistics GetGameStats(Guid gameId, IGameRepository repo) {
        return repo.GetOrCreateGameStats(gameId);
    }

    public RTPStatistics GetUserStats(Guid userId, IGameRepository repo) {
        return repo.GetOrCreateUserStats(userId);
    }
}
