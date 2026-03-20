using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IRealTimeService {
    Task NotifyJackpotUpdate(Jackpot jackpot);
    Task NotifyGameUpdate(Guid userId, object gameState);
    Task NotifyBalanceUpdate(Guid userId, decimal balance, decimal bonusBalance);
    Task NotifyRtpUpdate(Guid gameId, double currentRtp);
    Task NotifyProgressionUpdate(Guid userId, object progression);
    Task NotifyPrivateMessage(Guid senderId, Guid receiverId, string senderUsername, string message, string avatarUrl);
    
    // New Social Features
    Task NotifyBigWin(string username, string gameName, decimal amount, decimal multiplier);
    Task NotifyLeaderboardUpdate(string leaderboardName, object topList);
    Task NotifyAdminFeed(object adminEvent);
    Task NotifyAuditLog(AuditEvent auditEvent);
    Task BroadcastMessage(string sender, string message);
}
