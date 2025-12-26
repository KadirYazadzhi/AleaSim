using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AleaSim.Persistence;

public class EfGameRepository : IGameRepository {
    private readonly IServiceScopeFactory _scopeFactory;

    public EfGameRepository(IServiceScopeFactory scopeFactory) {
        _scopeFactory = scopeFactory;
    }

    private AleaSimDbContext CreateContext() {
        var scope = _scopeFactory.CreateScope();
        // Note: The scope needs to be disposed, but we are returning the Context. 
        // This is tricky. If we return context, we leak the scope unless we manage it.
        // Better pattern: ExecuteInContext(action).
        // But for this simple implementation, let's just create a new scope inside each method.
        // The CreateContext helper here is not useful if we need to Dispose.
        throw new NotImplementedException("Don't use this directly, use Using Scope pattern.");
    }

    public GameSession CreateSession(GameSession session) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        db.GameSessions.Add(session);
        db.SaveChanges();
        return session;
    }

    public GameSession? GetSession(Guid sessionId) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        return db.GameSessions.FirstOrDefault(s => s.Id == sessionId);
    }

    public void EndSession(Guid sessionId) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        var session = db.GameSessions.FirstOrDefault(s => s.Id == sessionId);
        if (session != null) {
            session.IsActive = false;
            session.EndedAt = DateTime.UtcNow;
            db.SaveChanges();
        }
    }

    public User? GetUser(Guid userId) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        return db.Users.FirstOrDefault(u => u.Id == userId);
    }

    public User? GetUserByUsername(string username) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        return db.Users.FirstOrDefault(u => u.Username == username);
    }

    public void CreateUser(User user) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        db.Users.Add(user);
        db.SaveChanges();
    }

    public void UpdateUserBalance(Guid userId, decimal amountToAdd) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        var user = db.Users.FirstOrDefault(u => u.Id == userId);
        if (user != null) {
            user.Balance += amountToAdd;
            db.SaveChanges();
        }
    }

    public void SaveBet(Bet bet) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        db.Bets.Add(bet);
        db.SaveChanges();
    }

    public void UpdateBet(Bet bet) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        db.Bets.Update(bet);
        db.SaveChanges();
    }

    public Bet? GetLastBet(Guid sessionId) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        return db.Bets.Where(b => b.GameSessionId == sessionId)
                      .OrderByDescending(b => b.CreatedAt)
                      .FirstOrDefault();
    }

    public int GetRoundCount(Guid sessionId) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        return db.GameRounds.Count(r => r.GameSessionId == sessionId);
    }

    public void SaveRound(GameRound round) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        // Check if exists to update or add
        if (db.GameRounds.Any(r => r.Id == round.Id)) {
            db.GameRounds.Update(round);
        } else {
            db.GameRounds.Add(round);
        }
        db.SaveChanges();
    }

    public GameRound? GetLastRound(Guid sessionId) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        return db.GameRounds.Where(r => r.GameSessionId == sessionId)
                            .OrderByDescending(r => r.ExecutedAt)
                            .FirstOrDefault();
    }

    public void SaveOutcome(Outcome outcome) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        db.Outcomes.Add(outcome);
        db.SaveChanges();
    }

    public Outcome? GetOutcome(Guid roundId) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        return db.Outcomes.FirstOrDefault(o => o.GameRoundId == roundId);
    }

    public RTPStatistics GetOrCreateGameStats(Guid gameId) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        var stats = db.RTPStatistics.FirstOrDefault(s => s.GameId == gameId);
        if (stats == null) {
            stats = new RTPStatistics { Id = Guid.NewGuid(), GameId = gameId, LastCalculated = DateTime.UtcNow };
            db.RTPStatistics.Add(stats);
            db.SaveChanges();
        }
        return stats;
    }

    public RTPStatistics GetOrCreateUserStats(Guid userId) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        var stats = db.RTPStatistics.FirstOrDefault(s => s.UserId == userId);
        if (stats == null) {
            stats = new RTPStatistics { Id = Guid.NewGuid(), UserId = userId, LastCalculated = DateTime.UtcNow };
            db.RTPStatistics.Add(stats);
            db.SaveChanges();
        }
        return stats;
    }

    public void UpdateRtpStats(Guid gameId, Guid userId, decimal bet, decimal win) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        
        var gStats = db.RTPStatistics.FirstOrDefault(s => s.GameId == gameId);
        if (gStats == null) {
            gStats = new RTPStatistics { Id = Guid.NewGuid(), GameId = gameId };
            db.RTPStatistics.Add(gStats);
        }
        
        var uStats = db.RTPStatistics.FirstOrDefault(s => s.UserId == userId);
        if (uStats == null) {
            uStats = new RTPStatistics { Id = Guid.NewGuid(), UserId = userId };
            db.RTPStatistics.Add(uStats);
        }

        gStats.TotalWagered += bet;
        gStats.TotalPaid += win;
        if (bet > 0) gStats.TotalRounds++;
        gStats.LastCalculated = DateTime.UtcNow;

        uStats.TotalWagered += bet;
        uStats.TotalPaid += win;
        if (bet > 0) uStats.TotalRounds++;
        uStats.LastCalculated = DateTime.UtcNow;

        db.SaveChanges();
    }

    public IEnumerable<RTPStatistics> GetAllRtpStats() {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        return db.RTPStatistics.ToList();
    }

    public Jackpot GetGlobalJackpot() {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        var jackpot = db.Jackpots.FirstOrDefault(j => j.IsGlobal);
        if (jackpot == null) {
            jackpot = new Jackpot { 
                Id = Guid.NewGuid(), 
                Name = "Global Grand Jackpot", 
                CurrentValue = 10000m, 
                ContributionRate = 0.01m, 
                IsGlobal = true, 
                LastUpdated = DateTime.UtcNow 
            };
            db.Jackpots.Add(jackpot);
            db.SaveChanges();
        }
        return jackpot;
    }

    public Jackpot GetOrCreateLocalJackpot(Guid gameId) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        var jackpot = db.Jackpots.FirstOrDefault(j => j.GameId == gameId && !j.IsGlobal);
        if (jackpot == null) {
            jackpot = new Jackpot {
                Id = Guid.NewGuid(),
                GameId = gameId,
                Name = "Local Jackpot",
                CurrentValue = 500m,
                ContributionRate = 0.005m,
                IsGlobal = false,
                LastUpdated = DateTime.UtcNow
            };
            db.Jackpots.Add(jackpot);
            db.SaveChanges();
        }
        return jackpot;
    }

    public void UpdateJackpot(Jackpot jackpot) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        var dbJackpot = db.Jackpots.FirstOrDefault(j => j.Id == jackpot.Id);
        if (dbJackpot != null) {
            dbJackpot.CurrentValue = jackpot.CurrentValue;
            dbJackpot.LastUpdated = DateTime.UtcNow;
            db.SaveChanges();
        }
    }

    public void SaveJackpotTrigger(Jackpot jackpot, decimal winAmount) {
         // Re-use update, mostly used to reset.
         UpdateJackpot(jackpot);
    }

    public void LogAudit(AuditEvent auditEvent) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        db.AuditLogs.Add(auditEvent);
        db.SaveChanges();
    }

    public IEnumerable<AuditEvent> GetAuditLogs(int count) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        return db.AuditLogs.OrderByDescending(x => x.Timestamp).Take(count).ToList();
    }

    public string? GetLastAuditHash() {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        return db.AuditLogs.OrderByDescending(x => x.Timestamp).Select(x => x.Hash).FirstOrDefault();
    }

    public Game? GetGameByType(string type) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        return db.Games.FirstOrDefault(g => g.Type == type);
    }

    public Game CreateGame(Game game) {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        db.Games.Add(game);
        db.SaveChanges();
        return game;
    }
}
