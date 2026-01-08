using AleaSim.Domain.Entities;
using AleaSim.Domain.Enums;
using AleaSim.Domain.Interfaces;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public interface IGameDirector {
    Task<GameSession> StartSession(string gameType, Guid userId, string? clientSeed = null);
    Task<GameRound> PlayRound(string gameType, Guid sessionId, decimal amount, object betData);
    Task<object> ProcessAction(string gameType, Guid sessionId, string action, string actionData);
}

public class GameDirector : IGameDirector {
    private readonly Func<string, IGame> _gameResolver;
    private readonly IGameRepository _repo;
    private readonly IAuditService _auditService;

    public GameDirector(Func<string, IGame> gameResolver, IGameRepository repo, IAuditService auditService) {
        _gameResolver = gameResolver;
        _repo = repo;
        _auditService = auditService;
    }

    public async Task<GameSession> StartSession(string gameType, Guid userId, string? clientSeed = null) {
        var engine = _gameResolver(gameType);
        return await engine.StartSession(userId, clientSeed: clientSeed);
    }

    public async Task<GameRound> PlayRound(string gameType, Guid sessionId, decimal amount, object betData) {
        var session = _repo.GetSession(sessionId);
        var user = session != null ? _repo.GetUser(session.UserId) : null;
        if (user != null && user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow) {
            throw new Exception($"Account is in cooldown until {user.LockoutUntil.Value:HH:mm:ss} UTC.");
        }
        if (amount <= 0) throw new ArgumentException("Bet amount must be positive.");
        var gameEngine = _gameResolver(gameType);
        
        var session = _repo.GetSession(sessionId);
        
        SpinProfile profile = SpinProfile.Standard;

        // Example: Whale Detection
        if (amount >= 50) {
            profile = SpinProfile.HighVolatility;
        }

        string betDataString = JsonSerializer.Serialize(betData);
        
        // 2. PLACE BET
        await gameEngine.PlaceBet(sessionId, amount, betDataString);

        // 3. EXECUTE WITH INSTRUCTION
        var round = await gameEngine.ResolveRound(sessionId, profile);

        _auditService.LogEvent("ROUND_PLAYED", $"{gameType} Round {round.RoundNumber} | Profile: {profile}", 
            session?.UserId.ToString() ?? "Unknown", 
            JsonSerializer.Serialize(new { Win = round.TotalWinAmount, Result = round.RandomResult }));

        return round;
    }

    public async Task<object> ProcessAction(string gameType, Guid sessionId, string action, string actionData) {
        var gameEngine = _gameResolver(gameType);
        await gameEngine.ProcessAction(sessionId, action, actionData);
        return await gameEngine.GetCurrentState(sessionId) ?? new { };
    }
}