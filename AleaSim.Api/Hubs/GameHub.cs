using Microsoft.AspNetCore.SignalR;
using AleaSim.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace AleaSim.Api.Hubs;

[Authorize]
public class GameHub : Hub {
    private readonly IAuditService _auditService;

    public GameHub(IAuditService auditService) {
        _auditService = auditService;
    }

    public async Task SendMessage(string message) {
        var username = Context.User?.Identity?.Name ?? "Anonymous";
        var userId = Context.UserIdentifier ?? "Unknown";

        // Broadcast to everyone
        await Clients.All.SendAsync("ReceiveChatMessage", username, message, DateTime.UtcNow);

        // Audit log for moderation
        _auditService.LogEvent("CHAT_MESSAGE", message, userId, message);
    }

    // Clients can join groups for specific games or sessions
    public async Task JoinGame(string gameType) {
        await Groups.AddToGroupAsync(Context.ConnectionId, gameType);
    }

    public async Task LeaveGame(string gameType) {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameType);
    }
}
