using AleaSim.Shared.Models;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AleaSim.Persistence;

public class EfGameRepository : IGameRepository {
    private readonly AleaSimDbContext _context;

    public EfGameRepository(AleaSimDbContext context) {
        _context = context;
    }

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

    public IEnumerable<GameSession> GetAllActiveSessions() {
        return _context.GameSessions.Where(s => s.IsActive).ToList();
    }

    
    public IEnumerable<ActiveSessionDto> GetActiveSessionsDetails() {
        var sessions = _context.GameSessions
            .Where(s => s.IsActive)
            .Join(_context.Users, s => s.UserId, u => u.Id, (s, u) => new { s, u })
            .Join(_context.Games, x => x.s.GameId, g => g.Id, (x, g) => new { x.s, x.u, g })
            .Select(x => new ActiveSessionDto {
                SessionId = x.s.Id,
                Username = x.u.Username,
                GameName = x.g.Name,
                StartedAt = x.s.StartedAt
            })
            .ToList();

        var sessionIds = sessions.Select(s => s.SessionId).ToList();

        var stats = _context.GameRounds
            .Where(r => sessionIds.Contains(r.GameSessionId))
            .GroupBy(r => r.GameSessionId)
            .Select(g => new { 
                SessionId = g.Key, 
                TotalBet = g.Sum(r => r.TotalBetAmount), 
                TotalWin = g.Sum(r => r.TotalWinAmount) 
            })
            .ToDictionary(k => k.SessionId, v => v);

        foreach (var session in sessions) {
            if (stats.TryGetValue(session.SessionId, out var stat)) {
                session.TotalWagered = stat.TotalBet;
                session.TotalWon = stat.TotalWin;
            }
        }

        return sessions;
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
        return _context.Games.FirstOrDefault(g => g.Type.ToLower() == gameType.ToLower());
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

    public IEnumerable<User> SearchUsers(string query) {
        return _context.Users
            .Where(u => u.Username.Contains(query) || u.Email.Contains(query))
            .Take(20)
            .ToList();
    }

    public void CreateUser(User user) {
        _context.Users.Add(user);
        _context.SaveChanges();
    }

    public void UpdateUserBalance(Guid userId, decimal amountToAdd) {
        var user = _context.Users.FirstOrDefault(u => u.Id == userId);
        if (user != null) {
            user.Balance += amountToAdd;
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
        return _context.PlayerProfiles
            .Include(p => p.User)
            .FirstOrDefault(p => p.UserId == userId);
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
        var targetDate = date.Date;
        var entry = _context.TournamentEntries
            .FirstOrDefault(t => t.UserId == userId && t.TournamentDate == targetDate);
            
        if (entry == null) {
            entry = new TournamentEntry { 
                Id = Guid.NewGuid(), 
                UserId = userId, 
                TournamentDate = targetDate,
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
        var targetDate = date.Date;
        return _context.TournamentEntries
            .Include(t => t.User)
            .Where(t => t.TournamentDate == targetDate)
            .AsEnumerable() // Move to memory to support ordering by computed property RoiPercentage
            .Where(t => !t.User.Username.StartsWith("Sim_"))
            .OrderByDescending(t => t.RoiPercentage)
            .Take(topCount)
            .ToList();
    }

    public IEnumerable<(Guid UserId, decimal NetResult)> CalculateDailyNet(DateTime date) {
        var start = date.Date;
        var end = start.AddDays(1);

        var stats = _context.GameRounds
            .Where(r => r.ExecutedAt >= start && r.ExecutedAt < end)
            .Join(_context.GameSessions, 
                  round => round.GameSessionId, 
                  session => session.Id, 
                  (round, session) => new { session.UserId, round.TotalBetAmount, round.TotalWinAmount })
            .Join(_context.Users, x => x.UserId, u => u.Id, (x, u) => new { x.UserId, x.TotalBetAmount, x.TotalWinAmount, u.Username, u.Role })
            .Where(x => !x.Username.StartsWith("Sim_") && x.Role != Role.Admin)
            .GroupBy(x => x.UserId)
            .Select(g => new { 
                UserId = g.Key, 
                NetResult = g.Sum(x => x.TotalWinAmount - x.TotalBetAmount) 
            })
            .AsEnumerable()
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

    public IEnumerable<GameRound> GetUserRounds(Guid userId, int count) {
        return _context.GameRounds
            .Join(_context.GameSessions,
                  round => round.GameSessionId,
                  session => session.Id,
                  (round, session) => new { round, session })
            .Where(x => x.session.UserId == userId)
            .OrderByDescending(x => x.round.ExecutedAt)
            .Take(count)
            .Select(x => x.round)
            .ToList();
    }

    
    public IEnumerable<GameRound> GetGlobalRecentRounds(int count) {
        return _context.GameRounds
            .Join(_context.GameSessions, r => r.GameSessionId, s => s.Id, (r, s) => new { r, s })
            .Join(_context.Users, x => x.s.UserId, u => u.Id, (x, u) => new { x.r, u })
            .Where(x => !x.u.Username.StartsWith("Sim_") && x.u.Role != Role.Admin)
            .OrderByDescending(x => x.r.ExecutedAt)
            .Take(count)
            .Select(x => x.r)
            .ToList();
    }

    
    public IEnumerable<GameRoundDto> GetUserHistory(Guid userId, int count) {
        return _context.GameRounds
            .Join(_context.GameSessions, r => r.GameSessionId, s => s.Id, (r, s) => new { r, s })
            .Where(x => x.s.UserId == userId)
            .Join(_context.Games, x => x.s.GameId, g => g.Id, (x, g) => new { x.r, x.s, g })
            .OrderByDescending(x => x.r.ExecutedAt)
            .Take(count)
            .Select(x => new GameRoundDto {
                Id = x.r.Id,
                GameName = x.g.Name,
                BetAmount = x.r.TotalBetAmount,
                WinAmount = x.r.TotalWinAmount,
                ResultSummary = x.r.DecisionType,
                FullResultJson = x.r.RandomResult,
                PlayedAt = x.r.ExecutedAt,
                ServerSeedHash = x.s.ServerSeedHash,
                ClientSeed = x.s.ClientSeed,
                Nonce = x.r.RoundNumber
            })
            .ToList();
    }

    public IEnumerable<GameRoundDto> GetGlobalHistory(int count) {
        return _context.GameRounds
            .Join(_context.GameSessions, r => r.GameSessionId, s => s.Id, (r, s) => new { r, s })
            .Join(_context.Users, x => x.s.UserId, u => u.Id, (x, u) => new { x.r, x.s, u })
            .Where(x => !x.u.Username.StartsWith("Sim_") && x.u.Role != Role.Admin)
            .Join(_context.Games, x => x.s.GameId, g => g.Id, (x, g) => new { x.r, x.s, g })
            .OrderByDescending(x => x.r.ExecutedAt)
            .Take(count)
            .Select(x => new GameRoundDto {
                Id = x.r.Id,
                GameName = x.g.Name,
                BetAmount = x.r.TotalBetAmount,
                WinAmount = x.r.TotalWinAmount,
                ResultSummary = x.r.DecisionType,
                FullResultJson = x.r.RandomResult,
                PlayedAt = x.r.ExecutedAt,
                ServerSeedHash = x.s.ServerSeedHash,
                ClientSeed = x.s.ClientSeed,
                Nonce = x.r.RoundNumber
            })
            .ToList();
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

    public IEnumerable<Jackpot> GetJackpots() {
        var jackpots = _context.Jackpots.ToList();
        if (!jackpots.Any(j => j.Tier == JackpotTier.Clubs)) {
            _context.Jackpots.AddRange(
                new Jackpot { Id = Guid.NewGuid(), Name = "Clubs", Tier = JackpotTier.Clubs, CurrentValue = 50, ContributionRate = 0.01m, IsGlobal = true, MustDropAt = 100, LastUpdated = DateTime.UtcNow },
                new Jackpot { Id = Guid.NewGuid(), Name = "Diamonds", Tier = JackpotTier.Diamonds, CurrentValue = 200, ContributionRate = 0.005m, IsGlobal = true, MustDropAt = 500, LastUpdated = DateTime.UtcNow },
                new Jackpot { Id = Guid.NewGuid(), Name = "Hearts", Tier = JackpotTier.Hearts, CurrentValue = 1000, ContributionRate = 0.002m, IsGlobal = true, MustDropAt = 2500, LastUpdated = DateTime.UtcNow },
                new Jackpot { Id = Guid.NewGuid(), Name = "Spades", Tier = JackpotTier.Spades, CurrentValue = 10000, ContributionRate = 0.001m, IsGlobal = true, MustDropAt = 50000, LastUpdated = DateTime.UtcNow }
            );
            _context.SaveChanges();
            return _context.Jackpots.ToList();
        }
        return jackpots;
    }

    public Jackpot GetGlobalJackpot() {
        // Fallback for legacy code
        return GetJackpots().First(j => j.Tier == JackpotTier.Spades);
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
                MustDropAt = 1000m,
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
            .Join(_context.Users, b => b.UserId, u => u.Id, (b, u) => new { b, u })
            .Where(x => !x.u.Username.StartsWith("Sim_") && x.u.Role != Role.Admin)
            .Sum(x => (decimal?)x.b.Amount) ?? 0m;
            
        var wins = _context.GameRounds
            .Where(r => r.ExecutedAt >= start && r.ExecutedAt < end)
            .Join(_context.GameSessions, r => r.GameSessionId, s => s.Id, (r, s) => new { r, s })
            .Join(_context.Users, x => x.s.UserId, u => u.Id, (x, u) => new { x.r, u })
            .Where(x => !x.u.Username.StartsWith("Sim_") && x.u.Role != Role.Admin)
            .Sum(x => (decimal?)x.r.TotalWinAmount) ?? 0m;
        
        return (bets, wins);
    }

    public decimal GetGlobalTotalRewardsPaid() {
        return _context.GameRounds
            .Join(_context.GameSessions, r => r.GameSessionId, s => s.Id, (r, s) => new { r, s })
            .Join(_context.Users, x => x.s.UserId, u => u.Id, (x, u) => new { x.r, u })
            .Where(x => !x.u.Username.StartsWith("Sim_") && x.u.Role != Role.Admin)
            .Sum(r => (decimal?)r.r.TotalWinAmount) ?? 0m;
    }

    public IEnumerable<(DateTime Hour, decimal Bets, decimal Wins)> GetRtpTrend(int hours) {
        var cutoff = DateTime.UtcNow.AddHours(-hours);
        
        return _context.GameRounds
            .Where(r => r.ExecutedAt >= cutoff)
            .Join(_context.GameSessions, r => r.GameSessionId, s => s.Id, (r, s) => new { r, s })
            .Join(_context.Users, x => x.s.UserId, u => u.Id, (x, u) => new { x.r, u })
            .Where(x => !x.u.Username.StartsWith("Sim_") && x.u.Role != Role.Admin)
            .AsEnumerable() 
            .GroupBy(r => new DateTime(r.r.ExecutedAt.Year, r.r.ExecutedAt.Month, r.r.ExecutedAt.Day, r.r.ExecutedAt.Hour, 0, 0))
            .Select(g => (
                Hour: g.Key,
                Bets: g.Sum(x => x.r.TotalBetAmount),
                Wins: g.Sum(x => x.r.TotalWinAmount)
            ))
            .OrderBy(x => x.Hour)
            .ToList();
    }

    public int GetActivePlayerCount(int minutes) {
        var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
        return _context.Users.Count(u => u.LastBetTimestamp >= cutoff && !u.Username.StartsWith("Sim_") && u.Role != Role.Admin);
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
            .Join(_context.Users, x => x.UserId, u => u.Id, (x, u) => new { x.UserId, x.TotalWinAmount, u.Username, u.Role })
            .Where(x => !x.Username.StartsWith("Sim_") && x.Role != Role.Admin)
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

    public UserProgression? GetUserProgression(Guid userId) {
        return _context.UserProgressions.FirstOrDefault(p => p.UserId == userId);
    }

    public void CreateUserProgression(UserProgression progression) {
        _context.UserProgressions.Add(progression);
        _context.SaveChanges();
    }

    public void UpdateUserProgression(UserProgression progression) {
        _context.UserProgressions.Update(progression);
        _context.SaveChanges();
    }

    public IEnumerable<Achievement> GetAchievementsByCondition(string conditionType) {
        return _context.Achievements.Where(a => a.ConditionType == conditionType).ToList();
    }

    public IEnumerable<UserAchievement> GetUserAchievements(Guid userId) {
        return _context.UserAchievements
            .Include(a => a.Achievement)
            .Where(a => a.UserId == userId)
            .ToList();
    }

    public void SaveUserAchievement(UserAchievement userAchievement) {
        _context.UserAchievements.Add(userAchievement);
        _context.SaveChanges();
    }

    public Voucher? GetVoucherByCode(string code) {
        return _context.Vouchers.FirstOrDefault(v => v.Code == code);
    }

    public bool HasUserRedeemedVoucher(Guid userId, Guid voucherId) {
        return _context.UserVouchers.Any(uv => uv.UserId == userId && uv.VoucherId == voucherId);
    }

    public void UpdateVoucher(Voucher voucher) {
        _context.Vouchers.Update(voucher);
        _context.SaveChanges();
    }

    public void SaveUserVoucher(UserVoucher userVoucher) {
        _context.UserVouchers.Add(userVoucher);
        _context.SaveChanges();
    }

    public void CreateVoucher(Voucher voucher) {
        _context.Vouchers.Add(voucher);
        _context.SaveChanges();
    }

    public IEnumerable<Voucher> GetAllVouchers() {
        return _context.Vouchers.ToList();
    }

    public void SaveTransaction(Transaction transaction) {
        _context.Transactions.Add(transaction);
        _context.SaveChanges();
    }

    public IEnumerable<Transaction> GetUserTransactions(Guid userId, int count) {
        return _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Timestamp)
            .Take(count)
            .ToList();
    }

    public string GetGlobalSetting(string key) {
        return _context.GlobalSettings.FirstOrDefault(s => s.Key == key)?.Value ?? string.Empty;
    }

    public void SaveTournamentWinners(IEnumerable<TournamentWinner> winners) {
        _context.TournamentWinners.AddRange(winners);
        _context.SaveChanges();
    }

    public IEnumerable<TournamentWinner> GetTournamentHistory(int months) {
        return _context.TournamentWinners
            .OrderByDescending(w => w.Month)
            .ThenBy(w => w.Rank)
            .Take(months * 10)
            .ToList();
    }

    public void SetGlobalSetting(string key, string value, string description) {
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

    public void DeleteUser(Guid userId) {
        var user = _context.Users.Find(userId);
        if (user != null) {
            // Manual cleanup to ensure no FK constraints block deletion
            var sessions = _context.GameSessions.Where(s => s.UserId == userId).ToList();
            var sessionIds = sessions.Select(s => s.Id).ToList();

            if (sessionIds.Any()) {
                var rounds = _context.GameRounds.Where(r => sessionIds.Contains(r.GameSessionId)).ToList();
                _context.GameRounds.RemoveRange(rounds);

                var bets = _context.Bets.Where(b => sessionIds.Contains(b.GameSessionId)).ToList();
                _context.Bets.RemoveRange(bets);
                
                _context.GameSessions.RemoveRange(sessions);
            }
            
            var profile = _context.PlayerProfiles.FirstOrDefault(p => p.UserId == userId);
            if (profile != null) _context.PlayerProfiles.Remove(profile);

            _context.Users.Remove(user);
            _context.SaveChanges();
        }
    }

}