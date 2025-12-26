using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public abstract class BaseGameEngine : IGame {
    protected readonly IRngService RngService;
    protected readonly IRtpEngine RtpEngine;
    protected readonly IJackpotService JackpotService;
    protected readonly IGameRepository Repository;

    protected BaseGameEngine(IRngService rngService, IRtpEngine rtpEngine, IJackpotService jackpotService, IGameRepository repository) {
        RngService = rngService;
        RtpEngine = rtpEngine;
        JackpotService = jackpotService;
        Repository = repository;
    }

    public virtual GameSession StartSession(Guid userId, int? seed = null) {
        var session = new GameSession {
            Id = Guid.NewGuid(),
            UserId = userId,
            Seed = seed ?? Guid.NewGuid().GetHashCode(),
            StartedAt = DateTime.UtcNow,
            IsActive = true,
            GameId = GetGameId()
        };
        
        Repository.CreateSession(session);
        return session;
    }

    public virtual void PlaceBet(Guid sessionId, decimal amount, string betData) {
        if (amount <= 0) throw new ArgumentException("Bet amount must be positive.");

        var session = Repository.GetSession(sessionId);
        if (session == null || !session.IsActive) {
            throw new InvalidOperationException("Session not found or inactive.");
        }

        var user = Repository.GetUser(session.UserId);
        if (user == null) throw new InvalidOperationException("User not found.");

        if (user.Balance < amount) {
            throw new InvalidOperationException("Insufficient balance.");
        }

        Repository.UpdateUserBalance(session.UserId, -amount);
        
        var bet = new Bet {
            Id = Guid.NewGuid(),
            GameSessionId = sessionId,
            UserId = session.UserId,
            Amount = amount,
            BetData = betData,
            CreatedAt = DateTime.UtcNow
        };
        Repository.SaveBet(bet);

        RtpEngine.RecordBet(session.GameId, session.UserId, amount);
        JackpotService.Contribute(session.GameId, amount);
    }

    public abstract GameRound ResolveRound(Guid sessionId);

    public abstract Outcome GetOutcome(Guid roundId);

    public virtual void ProcessAction(Guid sessionId, string action, string actionData) {
        // Default implementation does nothing
    }

    protected void EndSession(Guid sessionId) {
        Repository.EndSession(sessionId);
    }

    protected void UpdateBalance(Guid userId, decimal amount) {
        Repository.UpdateUserBalance(userId, amount);
    }
    
    protected Guid GetGameId() {
        string gameName = this.GetType().Name.Replace("GameEngine", "");
        var game = Repository.GetGameByType(gameName);
        
        if (game == null) {
            game = new Game { Id = Guid.NewGuid(), Name = gameName, Type = gameName };
            Repository.CreateGame(game);
        }
        return game.Id;
    }
}
