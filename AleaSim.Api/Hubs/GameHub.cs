using Microsoft.AspNetCore.SignalR;

namespace AleaSim.Api.Hubs;

public class GameHub : Hub {
    // Clients can join groups for specific games or sessions
    public async Task JoinGame(string gameType) {
        await Groups.AddToGroupAsync(Context.ConnectionId, gameType);
    }

    public async Task LeaveGame(string gameType) {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameType);
    }
}
