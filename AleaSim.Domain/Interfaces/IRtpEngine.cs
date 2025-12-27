using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IRtpEngine {
    bool IsOutcomeAllowed(Guid gameId, Guid userId, decimal potentialWinAmount, decimal betAmount, IGameRepository repo);
    void RecordBet(Guid gameId, Guid userId, decimal amount, IGameRepository repo);
    void RecordWin(Guid gameId, Guid userId, decimal amount, IGameRepository repo);
    RTPStatistics GetGameStats(Guid gameId, IGameRepository repo);
    RTPStatistics GetUserStats(Guid userId, IGameRepository repo);
}
