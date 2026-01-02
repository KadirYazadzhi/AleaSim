namespace AleaSim.Domain.Interfaces;

public interface IRealTimeService {
    Task NotifyJackpotUpdate(string name, decimal newValue);
    Task NotifyGameUpdate(Guid userId, object gameState);
    Task NotifyRtpUpdate(Guid gameId, double currentRtp);
    
    // New Social Features
    Task NotifyBigWin(string username, string gameName, decimal amount, decimal multiplier);
    Task NotifyLeaderboardUpdate(string leaderboardName, object topList);
}
