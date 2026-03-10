using AleaSim.Domain.Entities;
using AleaSim.Domain.Enums;
using AleaSim.Domain.Interfaces;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public interface IGameDirector {
    Task<GameSession> StartSession(string gameType, Guid userId, string? clientSeed = null);
    Task<GameSession> RotateSeed(string gameType, Guid userId);
    Task<GameRound> PlayRound(string gameType, Guid userId, Guid sessionId, decimal amount, object betData);
    Task<object> ProcessAction(string gameType, Guid userId, Guid sessionId, string action, object? actionData);
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

    public async Task<GameSession> RotateSeed(string gameType, Guid userId) {
        var game = _repo.GetGameByType(gameType);
        if (game == null) throw new Exception("Invalid game type");

        // 1. Find and close current active session
        var activeSessions = _repo.GetAllActiveSessions().Where(s => s.UserId == userId && s.GameId == game.Id).ToList();
        foreach (var s in activeSessions) {
            s.IsActive = false;
            s.EndedAt = DateTime.UtcNow;
            _repo.UpdateSession(s);
        }

        // 2. Clear Redis cache for session lookup
        string sessionCacheKey = $"active_session:{userId}:{game.Id}";
        await _repo.GetRedisCache().RemoveAsync(sessionCacheKey);

        // 3. Start a fresh session
        return await StartSession(gameType, userId);
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

            // Responsible Gaming: Daily Loss Limit Check
            if (amount > 0 && user.DailyLossLimit > 0) {
                decimal currentLoss = _repo.GetUserDailyLoss(userId, DateTime.UtcNow);
                if (currentLoss + amount > user.DailyLossLimit) {
                    throw new Exception($"Bet declined: Exceeds your daily loss limit of {user.DailyLossLimit:C}. Current net loss today: {currentLoss:C}.");
                }
            }
        }
        if (amount < 0) throw new ArgumentException("Bet amount cannot be negative.");

        var game = _repo.GetGameByType(gameType);
        if (game != null) {
            if (amount > 0 && amount < game.MinBet) throw new Exception($"Minimum bet for {game.Name} is {game.MinBet:C}.");
            if (amount > game.MaxBet) throw new Exception($"Maximum bet for {game.Name} is {game.MaxBet:C}.");
        }

        var gameEngine = _gameResolver(gameType);
        
        SpinProfile profile = SpinProfile.Standard;

        // Example: Whale Detection
        if (amount >= 50) {
            profile = SpinProfile.HighVolatility;
        }

        string betDataString = betData != null ? JsonSerializer.Serialize(betData) : "{}";
        
        // 2. PLACE BET (Now Secure)
        await gameEngine.PlaceBet(userId, sessionId, amount, betDataString);

        // 3. EXECUTE WITH INSTRUCTION
        var round = await gameEngine.ResolveRound(sessionId, profile);

        // ANALYTICS: Update Real-Time RTP Stats
        if (session != null) {
            _repo.UpdateRtpStats(session.GameId, session.UserId, amount, round.TotalWinAmount);
            _promotionService.ProcessWinActivity(session.UserId, round.TotalWinAmount, _repo);

            if (user != null && !user.Username.StartsWith("Sim_") && user.Role != Role.Admin) {
                _leaderboardService.SubmitScore(session.UserId, user.Username, round.TotalWinAmount, amount, gameType);
            }
        }

        _auditService.LogEvent("ROUND_PLAYED", $"{gameType} Round {round.RoundNumber} | Profile: {profile}", 
            session?.UserId.ToString() ?? "Unknown", 
            JsonSerializer.Serialize(new { Win = round.TotalWinAmount, Result = round.RandomResult }));

        return round;
    }

    public async Task<object> ProcessAction(string gameType, Guid userId, Guid sessionId, string action, object? actionData) {
        var gameEngine = _gameResolver(gameType);
        string dataString = actionData != null ? JsonSerializer.Serialize(actionData) : "{}";
        await gameEngine.ProcessAction(userId, sessionId, action, dataString);
        return await gameEngine.GetCurrentState(sessionId) ?? new { };
    }
}