using Microsoft.AspNetCore.SignalR;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace AleaSim.Api.Hubs;

[Authorize]
public class GameHub : Hub {
    private readonly IAuditService _auditService;
    private readonly IGameRepository _repo;
    private readonly IRealTimeService _realTimeService;

    public GameHub(IAuditService auditService, IGameRepository repo, IRealTimeService realTimeService) {
        _auditService = auditService;
        _repo = repo;
        _realTimeService = realTimeService;
    }

    public async Task SendMessage(string message) {
        var username = Context.User?.Identity?.Name ?? "Anonymous";
        var userIdString = Context.UserIdentifier ?? Guid.Empty.ToString();
        var userId = Guid.Parse(userIdString);
        
        var user = _repo.GetUser(userId);
        string avatarUrl = string.IsNullOrEmpty(user?.AvatarUrl) 
            ? $"https://api.dicebear.com/7.x/bottts/svg?seed={username}" 
            : user.AvatarUrl;

        var chatMsg = new ChatMessage {
            Id = Guid.NewGuid(),
            SenderId = userId,
            SenderUsername = username,
            SenderAvatarUrl = avatarUrl,
            Message = message,
            Timestamp = DateTime.UtcNow,
            Type = ChatMessageType.Global
        };

        _repo.SaveChatMessage(chatMsg);

        // Broadcast to everyone with avatar
        await Clients.All.SendAsync("ReceiveChatMessage", username, message, chatMsg.Timestamp, avatarUrl);

        // Audit log for moderation
        _auditService.LogEvent("CHAT_MESSAGE", message, userIdString, message);
    }

    public async Task SendPrivateMessage(Guid receiverId, string message) {
        var senderIdString = Context.UserIdentifier ?? Guid.Empty.ToString();
        var senderId = Guid.Parse(senderIdString);
        var sender = _repo.GetUser(senderId);
        
        if (sender == null) return;

        // Check if sender is admin OR receiver is admin
        var receiver = _repo.GetUser(receiverId);
        if (receiver == null) return;

        bool isSenderAdmin = sender.Role == AleaSim.Domain.Enums.Role.Admin;
        bool isReceiverAdmin = receiver.Role == AleaSim.Domain.Enums.Role.Admin;

        if (!isSenderAdmin && !isReceiverAdmin) {
            // Non-admins cannot send private messages to each other
            return;
        }

        string avatarUrl = string.IsNullOrEmpty(sender.AvatarUrl) 
            ? $"https://api.dicebear.com/7.x/bottts/svg?seed={sender.Username}" 
            : sender.AvatarUrl;

        var chatMsg = new ChatMessage {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            SenderUsername = sender.Username,
            SenderAvatarUrl = avatarUrl,
            ReceiverId = receiverId,
            Message = message,
            Timestamp = DateTime.UtcNow,
            Type = ChatMessageType.Private
        };

        _repo.SaveChatMessage(chatMsg);

        // Send via RealTimeService
        await _realTimeService.NotifyPrivateMessage(senderId, receiverId, sender.Username, message, avatarUrl);
    }

    // Clients can join groups for specific games or sessions
    public async Task JoinGame(string gameType) {
        await Groups.AddToGroupAsync(Context.ConnectionId, gameType);
    }

    public async Task LeaveGame(string gameType) {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameType);
    }

    public override async Task OnDisconnectedAsync(Exception? exception) {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Slot");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Roulette");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Blackjack");
        await base.OnDisconnectedAsync(exception);
    }
}