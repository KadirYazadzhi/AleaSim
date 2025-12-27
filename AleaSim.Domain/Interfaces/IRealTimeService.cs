namespace AleaSim.Domain.Interfaces;

public interface IRealTimeService {
    Task NotifyJackpotUpdate(string name, decimal newValue);
    Task NotifyGameUpdate(Guid userId, object gameState);
    Task NotifyRtpUpdate(Guid gameId, double currentRtp);
}
