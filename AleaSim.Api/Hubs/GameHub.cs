using Microsoft.AspNetCore.SignalR;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Services;
using Microsoft.AspNetCore.Authorization;

namespace AleaSim.Api.Hubs;

public class GameHub : Hub {
    private readonly IAuditService _auditService;
    private readonly IGameRepository _repo;
    private readonly IRealTimeService _realTimeService;
    private readonly IRedisCacheService _redisCache;

    public GameHub(IAuditService auditService, IGameRepository repo, IRealTimeService realTimeService, IRedisCacheService redisCache) {
        _auditService = auditService;
        _repo = repo;
        _realTimeService = realTimeService;
        _redisCache = redisCache;
    }

    [Authorize]
    public async Task SendMessage(string message) {
        var username = Context.User?.Identity?.Name ?? "Anonymous";
        var userIdString = Context.UserIdentifier ?? Guid.Empty.ToString();
        var userId = Guid.Parse(userIdString);
        
        // Rate Limiting: 2 seconds between messages
        if (await _redisCache.IncrementRateLimitAsync($"ratelimit:chat:{userId}", TimeSpan.FromSeconds(2), 1)) {
             return; // Silent drop for spam
        }

        string cleanMessage = Sanitize(message);
        if (string.IsNullOrWhiteSpace(cleanMessage)) return;

        var user = _repo.GetUser(userId);
        string avatarUrl = string.IsNullOrEmpty(user?.AvatarUrl) 
            ? $"https://api.dicebear.com/7.x/bottts/svg?seed={username}" 
            : user.AvatarUrl;

        var chatMsg = new ChatMessage {
            Id = Guid.NewGuid(),
            SenderId = userId,
            SenderUsername = username,
            SenderAvatarUrl = avatarUrl,
            Message = cleanMessage,
            Timestamp = DateTime.UtcNow,
            Type = ChatMessageType.Global
        };

        _repo.SaveChatMessage(chatMsg);

        // Broadcast to everyone with avatar
        await Clients.All.SendAsync("ReceiveChatMessage", username, cleanMessage, chatMsg.Timestamp, avatarUrl);

        // Audit log for moderation
        _auditService.LogEvent("CHAT_MESSAGE", cleanMessage, userIdString, cleanMessage);
    }

    [Authorize]
    public async Task SendPrivateMessage(Guid receiverId, string message) {
        var senderIdString = Context.UserIdentifier ?? Guid.Empty.ToString();
        var senderId = Guid.Parse(senderIdString);
        var sender = _repo.GetUser(senderId);
        
        if (sender == null) return;

        // Rate Limiting
        if (await _redisCache.IncrementRateLimitAsync($"ratelimit:pchat:{senderId}", TimeSpan.FromSeconds(1), 1)) {
             return;
        }

        string cleanMessage = Sanitize(message);
        if (string.IsNullOrWhiteSpace(cleanMessage)) return;

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
            Message = cleanMessage,
            Timestamp = DateTime.UtcNow,
            Type = ChatMessageType.Private
        };

        _repo.SaveChatMessage(chatMsg);

        // Send via RealTimeService
        await _realTimeService.NotifyPrivateMessage(senderId, receiverId, sender.Username, cleanMessage, avatarUrl);
    }

    private string Sanitize(string input) {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        
        // SECURITY: Use HtmlSanitizer library for proper XSS prevention
        var sanitizer = new Ganss.Xss.HtmlSanitizer();
        // Only allow safe tags
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.Add("b");
        sanitizer.AllowedTags.Add("i");
        sanitizer.AllowedTags.Add("em");
        sanitizer.AllowedTags.Add("strong");
        
        return sanitizer.Sanitize(input).Trim();
    }

    // Clients can join groups for specific games or sessions
    public async Task JoinGame(string gameType) {
        await Groups.AddToGroupAsync(Context.ConnectionId, gameType);
    }

    public async Task LeaveGame(string gameType) {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameType);
    }

    [Authorize]
    public async Task JoinAdminFeed() {
        if (Context.User?.IsInRole("Admin") == true) {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        }
    }

    [Authorize]
    public async Task LeaveAdminFeed() {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");
    }

    public override async Task OnConnectedAsync() {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId)) {
            var db = _redisCache.GetRedisDatabase();
            if (db != null) {
                // Use a Sorted Set for online users (Score = Timestamp)
                // This allows efficient cleanup of zombie sessions (Issue 33)
                await db.SortedSetAddAsync("presence:online_users", userId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                
                // Track individual connections for this user to support multi-tab (Issue 56)
                await db.SetAddAsync($"presence:connections:{userId}", Context.ConnectionId);
                await db.KeyExpireAsync($"presence:connections:{userId}", TimeSpan.FromHours(24));
            }
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception) {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId)) {
            var db = _redisCache.GetRedisDatabase();
            if (db != null) {
                await db.SetRemoveAsync($"presence:connections:{userId}", Context.ConnectionId);
                
                // Only remove from online_users if NO MORE active connections exist for this user
                long connectionCount = await db.SetLengthAsync($"presence:connections:{userId}");
                if (connectionCount == 0) {
                    await db.SortedSetRemoveAsync("presence:online_users", userId);
                }
            }
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Slot");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Roulette");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Blackjack");
        await base.OnDisconnectedAsync(exception);
    }
}