using AleaSim.Domain.Entities;
using AleaSim.Domain.Enums;
using AleaSim.Domain.Interfaces;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public interface IGameDirector {
    Task<GameSession> StartSession(string gameType, Guid userId, string? clientSeed = null);
    Task<GameRound> PlayRound(string gameType, Guid userId, Guid sessionId, decimal amount, object betData);
    Task<object> ProcessAction(string gameType, Guid userId, Guid sessionId, string action, string actionData);
    Task<object?> GetCurrentState(string gameType, Guid sessionId);
}

public class GameDirector : IGameDirector {
    private readonly Func<string, IGame> _gameResolver;
    private readonly IGameRepository _repo;
    private readonly IAuditService _auditService;
    private readonly IPromotionService _promotionService;
    private readonly ILeaderboardService _leaderboardService;

    public GameDirector(
        Func<string, IGame> gameResolver, 
        IGameRepository repo, 
        IAuditService auditService, 
        IPromotionService promotionService,
        ILeaderboardService leaderboardService) 
    {
        _gameResolver = gameResolver;
        _repo = repo;
        _auditService = auditService;
        _promotionService = promotionService;
        _leaderboardService = leaderboardService;
    }

    public async Task<object?> GetCurrentState(string gameType, Guid sessionId) {
        var engine = _gameResolver(gameType);
        return await engine.GetCurrentState(sessionId);
    }

    public async Task<GameSession> StartSession(string gameType, Guid userId, string? clientSeed = null) {
        var game = _repo.GetGameByType(gameType);
        if (game == null) throw new Exception("Invalid game type");

        var engine = _gameResolver(gameType);
        return await engine.StartSession(userId, game.Id, clientSeed: clientSeed);
    }

    public async Task<GameRound> PlayRound(string gameType, Guid userId, Guid sessionId, decimal amount, object betData) {
        var session = _repo.GetSession(sessionId);
        // Security check is done in engine, but we can do a quick check here too if session loaded
        if (session != null && session.UserId != userId) throw new UnauthorizedAccessException("Session mismatch");

        var user = session != null ? _repo.GetUser(session.UserId) : null;
        if (user != null) {
            if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow) {
                throw new Exception($"Account is in cooldown until {user.LockoutUntil.Value:HH:mm:ss} UTC.");
            }
            // Rate Limiting: 300ms minimum between spins
            if (user.LastBetTimestamp.HasValue && (DateTime.UtcNow - user.LastBetTimestamp.Value).TotalMilliseconds < 300) {
                 throw new Exception("You are spinning too fast! Please wait.");
            }
        }
        if (amount <= 0) throw new ArgumentException("Bet amount must be positive.");
        var gameEngine = _gameResolver(gameType);
        
        
        SpinProfile profile = SpinProfile.Standard;

        // Example: Whale Detection
        if (amount >= 50) {
            profile = SpinProfile.HighVolatility;
        }

        string betDataString = JsonSerializer.Serialize(betData);
        
        // 2. PLACE BET (Now Secure)
        await gameEngine.PlaceBet(userId, sessionId, amount, betDataString);

        // 3. EXECUTE WITH INSTRUCTION
        var round = await gameEngine.ResolveRound(sessionId, profile);

        // ANALYTICS: Update Real-Time RTP Stats
        if (session != null) {
            _repo.UpdateRtpStats(session.GameId, session.UserId, amount, round.TotalWinAmount);
            _promotionService.ProcessWinActivity(session.UserId, round.TotalWinAmount, _repo);

            if (user != null && !user.Username.StartsWith("Sim_")) {
                _leaderboardService.SubmitScore(session.UserId, user.Username, round.TotalWinAmount, amount, gameType);
            }
        }

        _auditService.LogEvent("ROUND_PLAYED", $"{gameType} Round {round.RoundNumber} | Profile: {profile}", 
            session?.UserId.ToString() ?? "Unknown", 
            JsonSerializer.Serialize(new { Win = round.TotalWinAmount, Result = round.RandomResult }));

        return round;
    }

    public async Task<object> ProcessAction(string gameType, Guid userId, Guid sessionId, string action, string actionData) {
        var gameEngine = _gameResolver(gameType);
        await gameEngine.ProcessAction(userId, sessionId, action, actionData);
        return await gameEngine.GetCurrentState(sessionId) ?? new { };
    }
}