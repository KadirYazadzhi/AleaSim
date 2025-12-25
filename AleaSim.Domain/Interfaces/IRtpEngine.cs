using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IRtpEngine {
    bool IsOutcomeAllowed(Guid gameId, Guid userId, decimal potentialWinAmount, decimal betAmount);
    void RecordBet(Guid gameId, Guid userId, decimal amount);
    void RecordWin(Guid gameId, Guid userId, decimal amount);
    RTPStatistics GetGameStats(Guid gameId);
    RTPStatistics GetUserStats(Guid userId);
}
