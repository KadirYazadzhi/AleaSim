using AleaSim.Domain.Entities;
using AleaSim.Domain.Enums;
using AleaSim.Domain.Interfaces;
using AleaSim.Shared.Models;
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
    private readonly IRealTimeService _realTime;
    private readonly IAchievementService _achievementService;

    public GameDirector(
        Func<string, IGame> gameResolver, 
        IGameRepository repo, 
        IAuditService auditService, 
        IPromotionService promotionService,
        ILeaderboardService leaderboardService,
        IRealTimeService realTime,
        IAchievementService achievementService) 
    {
        _gameResolver = gameResolver;
        _repo = repo;
        _auditService = auditService;
        _promotionService = promotionService;
        _leaderboardService = leaderboardService;
        _realTime = realTime;
        _achievementService = achievementService;
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
        string? revealedSeed = null;

        foreach (var s in activeSessions) {
            revealedSeed = s.ServerSeed; // Store before closing
            s.IsActive = false;
            s.EndedAt = DateTime.UtcNow;
            _repo.UpdateSession(s);
        }

        // 2. Clear Redis cache for session lookup
        string sessionCacheKey = $"active_session:{userId}:{game.Id}";
        await _repo.GetRedisCache().RemoveAsync(sessionCacheKey);

        // 3. Start a fresh session
        var newSession = await StartSession(gameType, userId);
        
        // Pass revealed seed back through memory (StartSession creates a new object, we attach revealed to it for the response)
        // Note: We use a custom field in StartSessionResponse, so we return the session and controller will wrap it.
        // We'll return the new session, but we need to pass the revealed one somehow.
        // Easiest is to return a tuple or just have the controller handle it if we modify engine?
        // Let's modify engine to potentially return the old seed or just store it in the new session object temporarily.
        newSession.GameState = revealedSeed ?? ""; // Temporary transport mechanism
        
        return newSession;
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
        if (amount <= 0) throw new ArgumentException("Bet amount must be greater than zero.");

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
            await _promotionService.ProcessWinActivity(session.UserId, round.TotalWinAmount, _repo);

            if (user != null && !user.Username.StartsWith("Sim_") && user.Role != Role.Admin) {
                _leaderboardService.SubmitScore(session.UserId, user.Username, round.TotalWinAmount, amount, gameType);
            }
        }

        _auditService.LogEvent("ROUND_PLAYED", $"{gameType} Round {round.RoundNumber} | Profile: {profile}", 
            session?.UserId.ToString() ?? "Unknown", 
            JsonSerializer.Serialize(new { Win = round.TotalWinAmount, Result = round.RandomResult }));

        // Re-fetch fresh user/profile to get exact sync values (prevents UI Drift)
        var freshUser = user != null ? _repo.GetUser(user.Id) : null;
        var freshProfile = user != null ? _repo.GetPlayerProfile(user.Id) : null;

        // BROADCAST TO ADMINS (SignalR Group: Admins)
        _ = _realTime.NotifyAdminFeed(new AdminRoundEvent {
            Timestamp = DateTime.UtcNow,
            Username = user?.Username ?? "Unknown",
            Game = gameType,
            Bet = amount,
            Win = round.TotalWinAmount,
            RoundNumber = round.RoundNumber,
            Decision = round.DecisionType,
            Multiplier = amount > 0 ? (double)(round.TotalWinAmount / (amount > 0 ? amount : 1)) : 0,
            
            // Sync current state
            Balance = freshUser?.Balance ?? 0,
            BonusBalance = freshUser?.BonusBalance ?? 0,
            LifetimeWagered = freshProfile?.TotalWagered ?? 0,
            LifetimeWon = freshProfile?.TotalPaid ?? 0
        });

        // ASYNC ACHIEVEMENT CHECK
        _ = Task.Run(() => _achievementService.CheckAchievements(userId, _repo, _realTime));

        return round;
    }

    public async Task<object> ProcessAction(string gameType, Guid userId, Guid sessionId, string action, object? actionData) {
        var gameEngine = _gameResolver(gameType);
        string dataString = actionData != null ? JsonSerializer.Serialize(actionData) : "{}";
        await gameEngine.ProcessAction(userId, sessionId, action, dataString);
        return await gameEngine.GetCurrentState(sessionId) ?? new { };
    }
}