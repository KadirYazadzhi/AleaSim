using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IRtpEngine {
        // Atomically checks if win is allowed and records it if successful
        bool ProcessWin(Guid gameId, Guid userId, decimal winAmount, decimal betAmount, IGameRepository repo);
        
        void RecordBet(Guid gameId, Guid userId, decimal amount, IGameRepository repo);
    
        RTPStatistics GetGameStats(Guid gameId, IGameRepository repo);
        RTPStatistics GetUserStats(Guid userId, IGameRepository repo);
}
