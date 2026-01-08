using Microsoft.AspNetCore.SignalR;
using AleaSim.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace AleaSim.Api.Hubs;

[Authorize]
public class GameHub : Hub {
    private readonly IAuditService _auditService;
    private readonly IGameRepository _repo;

    public GameHub(IAuditService auditService, IGameRepository repo) {
        _auditService = auditService;
        _repo = repo;
    }

    public async Task SendMessage(string message) {
        var username = Context.User?.Identity?.Name ?? "Anonymous";
        var userIdString = Context.UserIdentifier ?? Guid.Empty.ToString();
        var userId = Guid.Parse(userIdString);
        
        var user = _repo.GetUser(userId);
        string avatarUrl = user?.AvatarUrl ?? "https://api.dicebear.com/7.x/bottts/svg?seed=default";

        // Broadcast to everyone with avatar
        await Clients.All.SendAsync("ReceiveChatMessage", username, message, DateTime.UtcNow, avatarUrl);

        // Audit log for moderation
        _auditService.LogEvent("CHAT_MESSAGE", message, userIdString, message);
    }

    // Clients can join groups for specific games or sessions
    public async Task JoinGame(string gameType) {
        await Groups.AddToGroupAsync(Context.ConnectionId, gameType);
    }

    public async Task LeaveGame(string gameType) {
 
    public override async Task OnDisconnectedAsync(Exception? exception) {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Slot");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Roulette");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Blackjack");
        await base.OnDisconnectedAsync(exception);
    }
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameType);
    }
}
