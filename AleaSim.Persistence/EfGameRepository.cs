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
        var session = GetSession(sessionId);
        if (session != null) {
            session.EndedAt = DateTime.UtcNow;
            session.IsActive = false;
            _context.SaveChanges();
        }
    }

    public Game? GetGame(Guid gameId) {
        return _context.Games.Find(gameId);
    }

    public Game? GetGameByType(string gameType) {
        return _context.Games.FirstOrDefault(g => g.Type == gameType);
    }

    public void CreateGame(Game game) {
        _context.Games.Add(game);
        _context.SaveChanges();
    }

    public void UpdateGame(Game game) {
        _context.Games.Update(game);
        _context.SaveChanges();
    }

    public User? GetUser(Guid userId) {
        return _context.Users.FirstOrDefault(u => u.Id == userId);
    }

    public User? GetUserBySessionId(Guid sessionId) {
        var session = _context.GameSessions.FirstOrDefault(s => s.Id == sessionId);
        if (session == null) return null;
        return _context.Users.FirstOrDefault(u => u.Id == session.UserId);
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

    public void UpdateUser(User user) {
        _context.Users.Update(user);
        _context.SaveChanges();
    }

    public IEnumerable<User> GetUsersWithExpiredBonuses(DateTime cutoff) {
        return _context.Users
            .Where(u => u.BonusBalance > 0 && u.BonusLastUpdated != null && u.BonusLastUpdated < cutoff)
            .ToList();
    }

    public PlayerProfile? GetPlayerProfile(Guid userId) {
        return _context.PlayerProfiles.FirstOrDefault(p => p.UserId == userId);
    }

    public IEnumerable<PlayerProfile> GetActiveProfiles(TimeSpan activityWindow) {
        var threshold = DateTime.UtcNow - activityWindow;
        return _context.PlayerProfiles
            .Include(p => p.User)
            .Where(p => p.User.LastBetTimestamp >= threshold)
            .ToList();
    }

    public void CreatePlayerProfile(PlayerProfile profile) {
        _context.PlayerProfiles.Add(profile);
        _context.SaveChanges();
    }

    public void UpdatePlayerProfile(PlayerProfile profile) {
        _context.PlayerProfiles.Update(profile);
        _context.SaveChanges();
    }

    public TournamentEntry GetOrCreateTournamentEntry(Guid userId, DateTime date) {
        var entry = _context.TournamentEntries
            .FirstOrDefault(t => t.UserId == userId && t.TournamentDate.Date == date.Date);
            
        if (entry == null) {
            entry = new TournamentEntry { 
                Id = Guid.NewGuid(), 
                UserId = userId, 
                TournamentDate = date.Date,
                TotalWagered = 0,
                TotalPayout = 0
            };
            _context.TournamentEntries.Add(entry);
            _context.SaveChanges();
        }
        return entry;
    }

    public void UpdateTournamentEntry(TournamentEntry entry) {
        _context.TournamentEntries.Update(entry);
        _context.SaveChanges();
    }

    public IEnumerable<TournamentEntry> GetTopTournamentEntries(DateTime date, int topCount) {
        // Fetch into memory to calculate ROI property if needed, but better to order in DB if computed column.
        // ROI is computed property, so EF might not translate it directly unless configured.
        // For MVP, fetch all for the day and sort in memory (assuming not millions of players).
        return _context.TournamentEntries
            .Where(t => t.TournamentDate.Date == date.Date)
            .AsEnumerable() // Client-side evaluation for the computed property
            .OrderByDescending(t => t.RoiPercentage)
            .Take(topCount)
            .ToList();
    }

    public IEnumerable<(Guid UserId, decimal NetResult)> CalculateDailyNet(DateTime date) {
        var start = date.Date;
        var end = start.AddDays(1);

        // Optimized query joining GameRounds to GameSessions
        var stats = _context.GameRounds
            .Where(r => r.ExecutedAt >= start && r.ExecutedAt < end)
            .Join(_context.GameSessions, 
                  round => round.GameSessionId, 
                  session => session.Id, 
                  (round, session) => new { session.UserId, round.TotalBetAmount, round.TotalWinAmount })
            .GroupBy(x => x.UserId)
            .Select(g => new { 
                UserId = g.Key, 
                NetResult = g.Sum(x => x.TotalWinAmount - x.TotalBetAmount) 
            })
            .AsEnumerable() // Execute
            .Select(x => (x.UserId, x.NetResult))
            .ToList();

        return stats;
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
                MustDropAt = 15000m, // Guaranteed drop at 15k
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
                MustDropAt = 1000m, // Guaranteed drop at 1k
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

    public IEnumerable<AuditEvent> GetAllAuditLogs() {
        return _context.AuditLogs.OrderBy(x => x.Timestamp).ToList();
    }

    public string? GetLastAuditHash() {
        return _context.AuditLogs.OrderByDescending(x => x.Timestamp).Select(x => x.Hash).FirstOrDefault();
    }

    public (decimal TotalBets, decimal TotalWins) GetDailyFinancials(DateTime date) {
        var start = date.Date;
        var end = start.AddDays(1);
        
        var bets = _context.Bets
            .Where(b => b.CreatedAt >= start && b.CreatedAt < end)
            .Sum(b => (decimal?)b.Amount) ?? 0m;
            
        var wins = _context.GameRounds
            .Where(r => r.ExecutedAt >= start && r.ExecutedAt < end)
            .Sum(r => (decimal?)r.TotalWinAmount) ?? 0m;
        
        return (bets, wins);
    }

    public int GetActivePlayerCount(int minutes) {
        var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
        return _context.Users.Count(u => u.LastBetTimestamp >= cutoff);
    }

    public IEnumerable<(string Username, decimal TotalWin)> GetTopWinners(DateTime date, int topCount) {
        var start = date.Date;
        var end = start.AddDays(1);
        
        return _context.GameRounds
            .Where(r => r.ExecutedAt >= start && r.ExecutedAt < end && r.TotalWinAmount > 0)
            .Join(_context.GameSessions, 
                  r => r.GameSessionId, 
                  s => s.Id, 
                  (r, s) => new { s.UserId, r.TotalWinAmount })
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, TotalWin = g.Sum(x => x.TotalWinAmount) })
            .OrderByDescending(x => x.TotalWin)
            .Take(topCount)
            .ToList()
            .Join(_context.Users, 
                  stats => stats.UserId, 
                  user => user.Id, 
                  (stats, user) => (user.Username, stats.TotalWin));
    }

    public IEnumerable<Quest> GetActiveQuests(Guid userId) {
        return _context.Quests
            .Where(q => q.UserId == userId && q.Status != QuestStatus.Claimed && q.Status != QuestStatus.Expired && q.ExpiresAt > DateTime.UtcNow)
            .ToList();
    }

    public Quest? GetQuest(Guid questId) {
        return _context.Quests.Find(questId);
    }

    public void CreateQuest(Quest quest) {
        _context.Quests.Add(quest);
        _context.SaveChanges();
    }

    public void UpdateQuest(Quest quest) {
        _context.Quests.Update(quest);
        _context.SaveChanges();
    }

    public string GetGlobalSetting(string key) {
        var setting = _context.GlobalSettings.Find(key);
        return setting?.Value ?? string.Empty;
    }

    public void SetGlobalSetting(string key, string value, string description = "") {
        var setting = _context.GlobalSettings.Find(key);
        if (setting == null) {
            setting = new GlobalSetting { Key = key, Value = value, Description = description, LastUpdated = DateTime.UtcNow };
            _context.GlobalSettings.Add(setting);
        } else {
            setting.Value = value;
            if (!string.IsNullOrEmpty(description)) setting.Description = description;
            setting.LastUpdated = DateTime.UtcNow;
        }
        _context.SaveChanges();
    }
}