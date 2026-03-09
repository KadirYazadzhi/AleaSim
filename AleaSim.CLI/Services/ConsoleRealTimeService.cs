using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using System.Text.Json;

namespace AleaSim.CLI.Services;

public class ConsoleRealTimeService : IRealTimeService {
    public Task NotifyJackpotUpdate(Jackpot jackpot) {
        // To reduce noise, maybe only log when it changes significantly or just log debug
        // Console.WriteLine($"[RTS] Jackpot Update: {jackpot.Name} = {jackpot.CurrentValue:C}");
        return Task.CompletedTask;
    }

    public Task NotifyGameUpdate(Guid userId, object gameState) {
        string json = JsonSerializer.Serialize(gameState, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine($"\n[RTS] Game Update for {userId}:");
        Console.WriteLine(json);
        Console.WriteLine("------------------------------------------------");
        return Task.CompletedTask;
    }

    public Task NotifyBalanceUpdate(Guid userId, decimal balance, decimal bonusBalance) {
        Console.WriteLine($"[RTS] Balance Update: New Total = {(balance + bonusBalance):C} (Real: {balance:C}, Bonus: {bonusBalance:C})");
        return Task.CompletedTask;
    }

    public Task NotifyRtpUpdate(Guid gameId, double currentRtp) {
        // Console.WriteLine($"[RTS] RTP Update: {currentRtp:P2}");
        return Task.CompletedTask;
    }

    public Task NotifyProgressionUpdate(Guid userId, object progression) {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n[RTS] RPG PROGRESSION UPDATE:");
        Console.WriteLine(JsonSerializer.Serialize(progression, new JsonSerializerOptions { WriteIndented = true }));
        Console.ResetColor();
        return Task.CompletedTask;
    }

    public Task NotifyPrivateMessage(Guid senderId, Guid receiverId, string senderUsername, string message, string avatarUrl) {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"\n[PRIVATE MESSAGE] {senderUsername}: {message}");
        Console.ResetColor();
        return Task.CompletedTask;
    }

    public Task NotifyBigWin(string username, string gameName, decimal amount, decimal multiplier) {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n$$$ GLOBAL BIG WIN BROADCAST $$$");
        Console.WriteLine($"{username} just won {amount:C} ({multiplier:F0}x) on {gameName}!");
        Console.ResetColor();
        return Task.CompletedTask;
    }

    public Task NotifyLeaderboardUpdate(string leaderboardName, object topList) {
        // Console.WriteLine($"[RTS] Leaderboard Updated: {leaderboardName}");
        return Task.CompletedTask;
    }

    public Task BroadcastMessage(string sender, string message) {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n[SYSTEM BROADCAST from {sender}]: {message}");
        Console.ResetColor();
        return Task.CompletedTask;
    }
}
