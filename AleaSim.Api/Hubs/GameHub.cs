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
        
        // 1. Check if Chat is Enabled
        if (_repo.GetGlobalSetting("Community_ChatEnabled") == "false") {
            return;
        }

        // 2. Check Min Level Requirement
        if (int.TryParse(_repo.GetGlobalSetting("Community_MinLevelChat"), out var minLevel)) {
             var progression = _repo.GetPlayerProfile(userId)?.Progression;
             if (progression != null && progression.CurrentLevel < minLevel) {
                 return; // Silently drop
             }
        }

        // 3. Rate Limiting / Slow Mode
        int slowMode = 2;
        if (int.TryParse(_repo.GetGlobalSetting("Community_ChatSlowMode"), out var smVal)) {
            slowMode = Math.Max(1, smVal);
        }

        if (await _redisCache.IncrementRateLimitAsync($"ratelimit:chat:{userId}", TimeSpan.FromSeconds(slowMode), 1)) {
             return; // Silent drop for spam
        }

        // 4. Content Filtering (Prohibited Words)
        string cleanMessage = Sanitize(message);
        if (string.IsNullOrWhiteSpace(cleanMessage)) return;

        var prohibitedStr = _repo.GetGlobalSetting("Community_ProhibitedWords");
        if (!string.IsNullOrEmpty(prohibitedStr)) {
            var words = prohibitedStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var word in words) {
                if (cleanMessage.Contains(word, StringComparison.OrdinalIgnoreCase)) {
                    cleanMessage = new string('*', cleanMessage.Length); // Censor
                    break;
                }
            }
        }

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
        await Clients.All.SendAsync("ReceiveChatMessage", username, cleanMessage, chatMsg.Timestamp, avatarUrl, chatMsg.Id);

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
        await _realTimeService.NotifyPrivateMessage(senderId, receiverId, sender.Username, cleanMessage, avatarUrl, chatMsg.Id);
    }

    [Authorize]
    public async Task DeleteMessage(Guid messageId) {
        var userIdString = Context.UserIdentifier ?? Guid.Empty.ToString();
        var userId = Guid.Parse(userIdString);
        var user = _repo.GetUser(userId);
        if (user == null) return;

        var message = _repo.GetChatMessage(messageId);
        if (message == null || message.IsDeleted) return;

        bool isAdmin = user.Role == AleaSim.Domain.Enums.Role.Admin;
        if (message.SenderId != userId && !isAdmin) return; // Only sender or admin can delete

        message.IsDeleted = true;
        message.Message = "[Message Deleted]";
        _repo.UpdateChatMessage(message);

        // Broadcast deletion
        if (message.Type == ChatMessageType.Global) {
            await Clients.All.SendAsync("MessageDeleted", messageId);
        } else if (message.ReceiverId.HasValue) {
            // Send to both sender and receiver
            await Clients.Users(message.SenderId.ToString(), message.ReceiverId.Value.ToString()).SendAsync("MessageDeleted", messageId);
        }
    }

    [Authorize]
    public async Task EditMessage(Guid messageId, string newText) {
        var userIdString = Context.UserIdentifier ?? Guid.Empty.ToString();
        var userId = Guid.Parse(userIdString);
        
        string cleanMessage = Sanitize(newText);
        if (string.IsNullOrWhiteSpace(cleanMessage)) return;

        var message = _repo.GetChatMessage(messageId);
        if (message == null || message.IsDeleted) return;

        if (message.SenderId != userId) return; // Only sender can edit

        // 5 minute edit window
        if ((DateTime.UtcNow - message.Timestamp).TotalMinutes > 5) return;

        message.Message = cleanMessage;
        message.IsEdited = true;
        _repo.UpdateChatMessage(message);

        if (message.Type == ChatMessageType.Global) {
            await Clients.All.SendAsync("MessageEdited", messageId, cleanMessage);
        } else if (message.ReceiverId.HasValue) {
            await Clients.Users(message.SenderId.ToString(), message.ReceiverId.Value.ToString()).SendAsync("MessageEdited", messageId, cleanMessage);
        }
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