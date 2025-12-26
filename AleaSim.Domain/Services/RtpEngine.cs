using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class RtpEngine : IRtpEngine {
    private readonly IGameRepository _repository;
    private const double MaxAllowedRtpDeviation = 0.05; // Allow 5% deviation from target

    public RtpEngine(IGameRepository repository) {
        _repository = repository;
    }

    public bool IsOutcomeAllowed(Guid gameId, Guid userId, decimal potentialWinAmount, decimal betAmount) {
        var stats = _repository.GetOrCreateGameStats(gameId);
        
        // Target RTP logic (mocked target)
        double targetRtp = 0.95; 

        decimal projectedTotalPaid = stats.TotalPaid + potentialWinAmount;
        decimal projectedTotalWagered = stats.TotalWagered + betAmount;
        
        if (projectedTotalWagered == 0) return true;

        double projectedRtp = (double)(projectedTotalPaid / projectedTotalWagered);

        if (stats.TotalRounds > 1000 && projectedRtp > targetRtp + MaxAllowedRtpDeviation) {
            return false;
        }

        return true;
    }

    public void RecordBet(Guid gameId, Guid userId, decimal amount) {
        _repository.UpdateRtpStats(gameId, userId, amount, 0);
    }

    public void RecordWin(Guid gameId, Guid userId, decimal amount) {
        _repository.UpdateRtpStats(gameId, userId, 0, amount);
    }

    public RTPStatistics GetGameStats(Guid gameId) {
        return _repository.GetOrCreateGameStats(gameId);
    }

    public RTPStatistics GetUserStats(Guid userId) {
        return _repository.GetOrCreateUserStats(userId);
    }
}
