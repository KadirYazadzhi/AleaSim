using AleaSim.Api.Hubs;
using AleaSim.Domain.Interfaces;
using AleaSim.Shared.Models; // Added
using Microsoft.AspNetCore.SignalR;
using AleaSim.Domain.Entities; // Added

namespace AleaSim.Api.Services;

public class SignalRRealTimeService : IRealTimeService {
    private readonly IHubContext<GameHub> _hubContext;

    public SignalRRealTimeService(IHubContext<GameHub> hubContext) {
        _hubContext = hubContext;
    }

    public async Task NotifyJackpotUpdate(Jackpot jackpot) {
        var dto = new JackpotDto {
            Name = jackpot.Name,
            CurrentValue = jackpot.CurrentValue,
            MustDropAt = jackpot.MustDropAt,
            IsGlobal = jackpot.IsGlobal,
            Tier = jackpot.Tier.ToString()
        };
        await _hubContext.Clients.All.SendAsync("ReceiveJackpotUpdate", dto);
    }

    public async Task NotifyGameUpdate(Guid userId, object gameState) {
        // Securely send only to the specific user
        await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveGameUpdate", gameState);
    }

    public async Task NotifyBalanceUpdate(Guid userId, decimal balance, decimal bonusBalance) {
        await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveBalanceUpdate", new { Balance = balance, BonusBalance = bonusBalance });
    }

    public async Task NotifyRtpUpdate(Guid gameId, double currentRtp) {
        await _hubContext.Clients.All.SendAsync("ReceiveRtpUpdate", new { GameId = gameId, Rtp = currentRtp });
    }

    public async Task NotifyProgressionUpdate(Guid userId, object progression) {
        await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveProgressionUpdate", progression);
    }

    public async Task NotifyPrivateMessage(Guid senderId, Guid receiverId, string senderUsername, string message, string avatarUrl) {
        var time = DateTime.UtcNow;
        await _hubContext.Clients.User(receiverId.ToString()).SendAsync("ReceivePrivateMessage", senderUsername, message, time, avatarUrl, senderId);
        await _hubContext.Clients.User(senderId.ToString()).SendAsync("ReceivePrivateMessage", senderUsername, message, time, avatarUrl, senderId);
    }

    public async Task NotifyBigWin(string username, string gameName, decimal amount, decimal multiplier) {
        await _hubContext.Clients.All.SendAsync("ReceiveBigWin", new { 
            Username = username, 
            Game = gameName, 
            Amount = amount, 
            Multiplier = multiplier,
            Message = $"{username} just won {amount:C} ({multiplier:F0}x) on {gameName}!"
        });
    }

    public async Task NotifyLeaderboardUpdate(string leaderboardName, object topList) {
        await _hubContext.Clients.All.SendAsync("ReceiveLeaderboard", new { Name = leaderboardName, Data = topList });
    }

    public async Task NotifyAdminFeed(object adminEvent) {
        await _hubContext.Clients.Group("Admins").SendAsync("ReceiveAdminEvent", adminEvent);
    }

    public async Task BroadcastMessage(string sender, string message) {
        await _hubContext.Clients.All.SendAsync("ReceiveChatMessage", sender, message, DateTime.UtcNow, "https://cdn-icons-png.flaticon.com/512/1041/1041916.png");
    }
}
