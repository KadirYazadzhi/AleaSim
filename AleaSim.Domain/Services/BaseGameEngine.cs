using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using System.Collections.Concurrent;

namespace AleaSim.Domain.Services;

public abstract class BaseGameEngine : IGame {
    protected readonly IRngService RngService;
    protected readonly IRtpEngine RtpEngine;
    protected readonly IJackpotService JackpotService;
    protected readonly ConcurrentDictionary<Guid, GameSession> ActiveSessions = new();
    protected readonly ConcurrentDictionary<Guid, decimal> UserBalances = new(); // Mock balance storage

    protected BaseGameEngine(IRngService rngService, IRtpEngine rtpEngine, IJackpotService jackpotService) {
        RngService = rngService;
        RtpEngine = rtpEngine;
        JackpotService = jackpotService;
    }

    public virtual GameSession StartSession(Guid userId, int? seed = null) {
        var session = new GameSession {
            Id = Guid.NewGuid(),
            UserId = userId,
            Seed = seed ?? Guid.NewGuid().GetHashCode(),
            StartedAt = DateTime.UtcNow,
            IsActive = true
        };
        ActiveSessions[session.Id] = session;
        return session;
    }

    public virtual void PlaceBet(Guid sessionId, decimal amount, string betData) {
        if (!ActiveSessions.TryGetValue(sessionId, out var session) || !session.IsActive) {
            throw new InvalidOperationException("Session not found or inactive.");
        }

        if (amount <= 0) throw new ArgumentException("Bet amount must be positive.");

        // Simulate balance check
        var balance = UserBalances.GetOrAdd(session.UserId, 1000m); // Default 1000 for simulation
        if (balance < amount) {
            throw new InvalidOperationException("Insufficient balance.");
        }

        UserBalances[session.UserId] -= amount;
        RtpEngine.RecordBet(session.GameId, session.UserId, amount);
        JackpotService.Contribute(session.GameId, amount);
    }

    public abstract GameRound ResolveRound(Guid sessionId);

    public abstract Outcome GetOutcome(Guid roundId);

    public virtual void ProcessAction(Guid sessionId, string action, string actionData) {
        // Default implementation does nothing
    }

    protected void EndSession(Guid sessionId) {
        if (ActiveSessions.TryGetValue(sessionId, out var session)) {
            session.IsActive = false;
            session.EndedAt = DateTime.UtcNow;
        }
    }

    protected void UpdateBalance(Guid userId, decimal amount) {
        UserBalances.AddOrUpdate(userId, amount, (key, old) => old + amount);
    }
}
