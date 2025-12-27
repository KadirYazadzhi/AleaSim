using AleaSim.Api.Hubs;
using AleaSim.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace AleaSim.Api.Services;

public class SignalRRealTimeService : IRealTimeService {
    private readonly IHubContext<GameHub> _hubContext;

    public SignalRRealTimeService(IHubContext<GameHub> hubContext) {
        _hubContext = hubContext;
    }

    public async Task NotifyJackpotUpdate(string name, decimal newValue) {
        await _hubContext.Clients.All.SendAsync("ReceiveJackpotUpdate", new { Name = name, Value = newValue });
    }

    public async Task NotifyGameUpdate(Guid userId, object gameState) {
        // Securely send only to the specific user
        await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveGameUpdate", gameState);
    }

    public async Task NotifyRtpUpdate(Guid gameId, double currentRtp) {
        await _hubContext.Clients.All.SendAsync("ReceiveRtpUpdate", new { GameId = gameId, Rtp = currentRtp });
    }
}
