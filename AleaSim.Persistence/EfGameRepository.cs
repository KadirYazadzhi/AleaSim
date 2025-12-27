using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AleaSim.Persistence;

public class EfGameRepository : IGameRepository {
    private readonly AleaSimDbContext _context;

    public EfGameRepository(AleaSimDbContext context) {
        _context = context;
    }

    // Helper for explicit transactions if needed outside of SaveChanges
    public ITransaction BeginTransaction() {
        return new EfTransactionWrapper(_context.Database.BeginTransaction());
    }

    public void SaveChanges() {
        _context.SaveChanges();
    }

    public GameSession CreateSession(GameSession session) {
        _context.GameSessions.Add(session);
        _context.SaveChanges();
        return session;
    }

    public GameSession? GetSession(Guid sessionId) {
        return _context.GameSessions.FirstOrDefault(s => s.Id == sessionId);
    }

    public void EndSession(Guid sessionId) {
        var session = _context.GameSessions.FirstOrDefault(s => s.Id == sessionId);
        if (session != null) {
            session.IsActive = false;
            session.EndedAt = DateTime.UtcNow;
            _context.SaveChanges();
        }
    }

    public User? GetUser(Guid userId) {
        return _context.Users.FirstOrDefault(u => u.Id == userId);
    }

    public User? GetUserByUsername(string username) {
        return _context.Users.FirstOrDefault(u => u.Username == username);
    }

    public void CreateUser(User user) {
        _context.Users.Add(user);
        _context.SaveChanges();
    }

    public void UpdateUserBalance(Guid userId, decimal amountToAdd) {
        var user = _context.Users.FirstOrDefault(u => u.Id == userId);
        if (user != null) {
            user.Balance += amountToAdd;
            // Note: We don't SaveChanges here automatically to allow batching in transaction, 
            // OR we save, and transaction rollback reverts it.
            // For safety in this hybrid approach, we Save. Transaction will rollback if needed.
            _context.SaveChanges();
        }
    }

    public void SaveBet(Bet bet) {
        _context.Bets.Add(bet);
        _context.SaveChanges();
    }

    public void UpdateBet(Bet bet) {
        _context.Bets.Update(bet);
        _context.SaveChanges();
    }

    public Bet? GetLastBet(Guid sessionId) {
        return _context.Bets.Where(b => b.GameSessionId == sessionId)
                      .OrderByDescending(b => b.CreatedAt)
                      .FirstOrDefault();
    }

    public int GetRoundCount(Guid sessionId) {
        return _context.GameRounds.Count(r => r.GameSessionId == sessionId);
    }

    public void SaveRound(GameRound round) {
        if (_context.GameRounds.Any(r => r.Id == round.Id)) {
            _context.GameRounds.Update(round);
        } else {
            _context.GameRounds.Add(round);
        }
        _context.SaveChanges();
    }

    public GameRound? GetLastRound(Guid sessionId) {
        return _context.GameRounds.Where(r => r.GameSessionId == sessionId)
                            .OrderByDescending(r => r.ExecutedAt)
                            .FirstOrDefault();
    }

    public void SaveOutcome(Outcome outcome) {
        _context.Outcomes.Add(outcome);
        _context.SaveChanges();
    }

    public Outcome? GetOutcome(Guid roundId) {
        return _context.Outcomes.FirstOrDefault(o => o.GameRoundId == roundId);
    }

    public RTPStatistics GetOrCreateGameStats(Guid gameId) {
        var stats = _context.RTPStatistics.FirstOrDefault(s => s.GameId == gameId);
        if (stats == null) {
            stats = new RTPStatistics { Id = Guid.NewGuid(), GameId = gameId, LastCalculated = DateTime.UtcNow };
            _context.RTPStatistics.Add(stats);
            _context.SaveChanges();
        }
        return stats;
    }

    public RTPStatistics GetOrCreateUserStats(Guid userId) {
        var stats = _context.RTPStatistics.FirstOrDefault(s => s.UserId == userId);
        if (stats == null) {
            stats = new RTPStatistics { Id = Guid.NewGuid(), UserId = userId, LastCalculated = DateTime.UtcNow };
            _context.RTPStatistics.Add(stats);
            _context.SaveChanges();
        }
        return stats;
    }

    public void UpdateRtpStats(Guid gameId, Guid userId, decimal bet, decimal win) {
        // Here we rely on tracking. If we fetched them before in same scope, they are tracked.
        // We re-fetch to be safe or use attached entities.
        var gStats = GetOrCreateGameStats(gameId);
        var uStats = GetOrCreateUserStats(userId);

        gStats.TotalWagered += bet;
        gStats.TotalPaid += win;
        if (bet > 0) gStats.TotalRounds++;
        gStats.LastCalculated = DateTime.UtcNow;

        uStats.TotalWagered += bet;
        uStats.TotalPaid += win;
        if (bet > 0) uStats.TotalRounds++;
        uStats.LastCalculated = DateTime.UtcNow;

        _context.SaveChanges();
    }

    public IEnumerable<RTPStatistics> GetAllRtpStats() {
        return _context.RTPStatistics.ToList();
    }

    public Jackpot GetGlobalJackpot() {
        var jackpot = _context.Jackpots.FirstOrDefault(j => j.IsGlobal);
        if (jackpot == null) {
            jackpot = new Jackpot { 
                Id = Guid.NewGuid(), 
                Name = "Global Grand Jackpot", 
                CurrentValue = 10000m, 
                ContributionRate = 0.01m, 
                IsGlobal = true, 
                LastUpdated = DateTime.UtcNow 
            };
            _context.Jackpots.Add(jackpot);
            _context.SaveChanges();
        }
        return jackpot;
    }

    public Jackpot GetOrCreateLocalJackpot(Guid gameId) {
        var jackpot = _context.Jackpots.FirstOrDefault(j => j.GameId == gameId && !j.IsGlobal);
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
            _context.Jackpots.Add(jackpot);
            _context.SaveChanges();
        }
        return jackpot;
    }

    public void UpdateJackpot(Jackpot jackpot) {
        _context.Jackpots.Update(jackpot);
        _context.SaveChanges();
    }

    public void SaveJackpotTrigger(Jackpot jackpot, decimal winAmount) {
         UpdateJackpot(jackpot);
    }

    public void LogAudit(AuditEvent auditEvent) {
        _context.AuditLogs.Add(auditEvent);
        _context.SaveChanges();
    }

    public IEnumerable<AuditEvent> GetAuditLogs(int count) {
        return _context.AuditLogs.OrderByDescending(x => x.Timestamp).Take(count).ToList();
    }

    public string? GetLastAuditHash() {
        return _context.AuditLogs.OrderByDescending(x => x.Timestamp).Select(x => x.Hash).FirstOrDefault();
    }

    public Game? GetGameByType(string type) {
        return _context.Games.FirstOrDefault(g => g.Type == type);
    }

    public Game CreateGame(Game game) {
        _context.Games.Add(game);
        _context.SaveChanges();
        return game;
    }
}
