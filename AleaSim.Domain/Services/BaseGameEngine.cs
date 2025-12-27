using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AleaSim.Domain.Services;

public abstract class BaseGameEngine : IGame {
    protected readonly IRngService RngService;
    protected readonly IRtpEngine RtpEngine;
    protected readonly IJackpotService JackpotService;
    protected readonly IRealTimeService RealTimeService; // Added
    protected readonly IServiceScopeFactory ScopeFactory;

    protected BaseGameEngine(IRngService rngService, IRtpEngine rtpEngine, IJackpotService jackpotService, IRealTimeService realTimeService, IServiceScopeFactory scopeFactory) {
        RngService = rngService;
        RtpEngine = rtpEngine;
        JackpotService = jackpotService;
        RealTimeService = realTimeService;
        ScopeFactory = scopeFactory;
    }

    // Helper to execute logic in a scope with repository
    protected T ExecuteScoped<T>(Func<IGameRepository, T> action) {
        using var scope = ScopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        return action(repo);
    }

    protected void ExecuteScoped(Action<IGameRepository> action) {
        using var scope = ScopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        action(repo);
    }

    public virtual GameSession StartSession(Guid userId, int? seed = null) {
        return ExecuteScoped(repo => {
            var session = new GameSession {
                Id = Guid.NewGuid(),
                UserId = userId,
                Seed = seed ?? Guid.NewGuid().GetHashCode(),
                StartedAt = DateTime.UtcNow,
                IsActive = true,
                GameId = GetGameId(repo)
            };
            repo.CreateSession(session);
            return session;
        });
    }

    public virtual void PlaceBet(Guid sessionId, decimal amount, string betData) {
        if (amount <= 0) throw new ArgumentException("Bet amount must be positive.");

        ExecuteScoped(repo => {
            using var transaction = repo.BeginTransaction();
            try {
                var session = repo.GetSession(sessionId);
                if (session == null || !session.IsActive) {
                    throw new InvalidOperationException("Session not found or inactive.");
                }

                var user = repo.GetUser(session.UserId);
                if (user == null) throw new InvalidOperationException("User not found.");

                if (user.Balance < amount) {
                    throw new InvalidOperationException("Insufficient balance.");
                }

                repo.UpdateUserBalance(session.UserId, -amount);
                
                var bet = new Bet {
                    Id = Guid.NewGuid(),
                    GameSessionId = sessionId,
                    UserId = session.UserId,
                    Amount = amount,
                    BetData = betData,
                    CreatedAt = DateTime.UtcNow
                };
                repo.SaveBet(bet);

                // Note: RtpEngine and JackpotService usually take Repo as dependency.
                // BUT they are Singletons and expect to be Scoped-aware or take Repo as param?
                // Currently they take IGameRepository in constructor.
                // THIS IS A PROBLEM. RtpEngine is Singleton but IGameRepository is Scoped.
                // We must change RtpEngine/JackpotService to be Scoped OR to take Repo as method param.
                // Ideally, we pass the current 'repo' instance to them manually or change them to helper classes.
                // OR: RtpEngine/JackpotService should also use ScopeFactory internally?
                // NO, if we are in a transaction, they MUST use the SAME repo instance.
                
                // FIX: RtpEngine and JackpotService methods should accept IGameRepository.
                // OR: We move the logic here.
                
                // Temporary HACK: We will assume RtpEngine/JackpotService are refactored to take Repo.
                RtpEngine.RecordBet(session.GameId, session.UserId, amount, repo);
                JackpotService.Contribute(session.GameId, amount, repo);
                
                transaction.Commit();
            }
            catch {
                transaction.Rollback();
                throw;
            }
        });
    }

    public abstract GameRound ResolveRound(Guid sessionId);

    public abstract Outcome GetOutcome(Guid roundId);

    public virtual void ProcessAction(Guid sessionId, string action, string actionData) {
        // Default implementation does nothing
    }

    public virtual object? GetCurrentState(Guid sessionId) {
        return ExecuteScoped<object?>(repo => {
             // Basic implementation, overriden by Blackjack
             return null; 
        });
    }

    protected void EndSession(Guid sessionId) {
        ExecuteScoped(repo => repo.EndSession(sessionId));
    }

    protected void UpdateBalance(Guid userId, decimal amount, IGameRepository repo) {
        repo.UpdateUserBalance(userId, amount);
    }
    
    protected Guid GetGameId(IGameRepository repo) {
        string gameName = this.GetType().Name.Replace("GameEngine", "");
        var game = repo.GetGameByType(gameName);
        
        if (game == null) {
            game = new Game { Id = Guid.NewGuid(), Name = gameName, Type = gameName };
            repo.CreateGame(game);
        }
        return game.Id;
    }
}
