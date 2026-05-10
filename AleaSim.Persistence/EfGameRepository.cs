using AleaSim.Shared.Models;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using AleaSim.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AleaSim.Persistence;

public class EfGameRepository : IGameRepository {
    private readonly AleaSimDbContext _context;
    private readonly IRedisCacheService _redisCache;

    public EfGameRepository(AleaSimDbContext context, IRedisCacheService redisCache) {
        _context = context;
        _redisCache = redisCache;
    }

    public ITransaction BeginTransaction() {
        if (_context.Database.CurrentTransaction != null) {
            return new NullTransaction();
        }
        return new EfTransactionWrapper(_context.Database.BeginTransaction());
    }

    public void SaveChanges() {
        _context.SaveChanges();
    }

    public GameSession CreateSession(GameSession session) {
        _context.GameSessions.Add(session);
        _context.SaveChanges();
        
        // Cache session in Redis (TTL 2 hours)
        _ = _redisCache.SetAsync($"session:{session.Id}", session, TimeSpan.FromHours(2));
        return session;
    }

    public async Task<GameSession> CreateSessionAsync(GameSession session) {
        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync();
        await _redisCache.SetAsync($"session:{session.Id}", session, TimeSpan.FromHours(2));
        return session;
    }

    public GameSession? GetSession(Guid sessionId) {
        // PERFORMANCE: Query DB directly in sync method to avoid thread pool starvation from .GetAwaiter().GetResult()
        // The async counterpart GetSessionAsync already handles caching properly.
        var session = _context.GameSessions.FirstOrDefault(s => s.Id == sessionId);
        
        if (session != null) {
            _ = _redisCache.SetAsync($"session:{sessionId}", session, TimeSpan.FromHours(2));
        }
        return session;
    }

    public async Task<GameSession?> GetSessionAsync(Guid sessionId) {
        var session = await _redisCache.GetAsync<GameSession>($"session:{sessionId}");
        if (session != null) return session;

        session = await _context.GameSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session != null) {
            await _redisCache.SetAsync($"session:{sessionId}", session, TimeSpan.FromHours(2));
        }
        return session;
    }

    public void UpdateSession(GameSession session) {
        session.LastActivityAt = DateTime.UtcNow;
        _context.GameSessions.Update(session);
        _context.SaveChanges();
        _ = _redisCache.SetAsync($"session:{session.Id}", session, TimeSpan.FromHours(2));
    }

    public async Task UpdateSessionAsync(GameSession session) {
        session.LastActivityAt = DateTime.UtcNow;
        _context.GameSessions.Update(session);
        await _context.SaveChangesAsync();
        await _redisCache.SetAsync($"session:{session.Id}", session, TimeSpan.FromHours(2));
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
                StartedAt = x.s.StartedAt,
                TotalWagered = x.s.TotalWagered,
                TotalWon = x.s.TotalWon
            })
            .ToList();

        // Fix for Issue 38: Uses pre-aggregated stats from GameSession entity for high performance.
        return sessions;
    }

    public void EndSession(Guid sessionId) {
        var session = _context.GameSessions.FirstOrDefault(s => s.Id == sessionId);
        if (session != null) {
            session.EndedAt = DateTime.UtcNow;
            session.IsActive = false;
            _context.SaveChanges();
            
            // Remove from cache
            _redisCache.RemoveAsync($"session:{sessionId}");
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

    public void UpdateGamePoolBalance(Guid gameId, decimal amountToAdd) {
        // Atomic update to prevent race conditions on global pool balance
        _context.Database.ExecuteSqlRaw(
            "UPDATE Games SET PoolBalance = PoolBalance + {0} WHERE Id = {1}",
            amountToAdd, gameId);
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

    public User? GetUserByReferralCode(string code) {
        return _context.Users.FirstOrDefault(u => u.ReferralCode == code);
    }

    public IEnumerable<User> SearchUsers(string query) {
        return _context.Users
            .Include(u => u.Profile)
            .Where(u => u.Username.Contains(query) || u.Email.Contains(query))
            .Take(50)
            .ToList();
    }

    public IEnumerable<User> GetAllUsers() {
        return _context.Users.ToList();
    }

    public int GetTotalUserCount() {
        return _context.Users.Count(u => u.Role != Role.Admin && !u.Username.StartsWith("Sim_"));
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
        var existing = _context.Users.Local.FirstOrDefault(u => u.Id == user.Id);
        if (existing != null) {
            _context.Entry(existing).State = EntityState.Detached;
        }
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
        var existing = _context.PlayerProfiles.Local.FirstOrDefault(p => p.Id == profile.Id);
        if (existing != null) {
            _context.Entry(existing).State = EntityState.Detached;
        }
        _context.PlayerProfiles.Update(profile);
        _context.SaveChanges();
    }

    public TournamentEntry GetOrCreateTournamentEntry(Guid userId, DateTime date) {
        // Normalize to the start of the month for monthly tournaments
        var targetDate = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
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
        var targetDate = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return _context.TournamentEntries
            .Include(t => t.User)
            .Where(t => t.TournamentDate == targetDate)
            .AsEnumerable() 
            .Where(t => t.User != null && !t.User.Username.StartsWith("Sim_") && t.User.Role != Role.Admin)
            .OrderByDescending(t => t.RoiPercentage)
            .Take(topCount)
            .ToList();
    }

    public decimal GetUserDailyLoss(Guid userId, DateTime date) {
        var start = date.Date;
        var end = start.AddDays(1);

        var netResult = _context.GameRounds
            .Where(r => r.ExecutedAt >= start && r.ExecutedAt < end)
            .Join(_context.GameSessions, 
                  round => round.GameSessionId, 
                  session => session.Id, 
                  (round, session) => new { session.UserId, round.TotalBetAmount, round.TotalWinAmount })
            .Where(x => x.UserId == userId)
            .Sum(x => (decimal?)(x.TotalBetAmount - x.TotalWinAmount)) ?? 0m;

        return netResult;
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

    public IEnumerable<Quest> GetAllQuests() => _context.Quests.ToList();
    public IEnumerable<UserQuestProgress> GetUserQuestProgressions(Guid userId) => _context.UserQuestProgressions.Where(p => p.UserId == userId).ToList();
    public void CreateUserQuestProgress(UserQuestProgress progress) { _context.UserQuestProgressions.Add(progress); _context.SaveChanges(); }
    public void UpdateUserQuestProgress(UserQuestProgress progress) {
        var existing = _context.UserQuestProgressions.Local.FirstOrDefault(p => p.Id == progress.Id);
        if (existing != null) {
            _context.Entry(existing).State = EntityState.Detached;
        }
        _context.UserQuestProgressions.Update(progress);
        _context.SaveChanges();
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
                ServerSeed = x.s.IsActive ? null : x.s.ServerSeed,
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
                ServerSeed = x.s.IsActive ? null : x.s.ServerSeed,
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

        int roundInc = bet > 0 ? 1 : 0;
        var now = DateTime.UtcNow;

        // Atomic update for Game Stats
        _context.Database.ExecuteSqlInterpolated(
            $"UPDATE RTPStatistics SET TotalWagered = TotalWagered + {bet}, TotalPaid = TotalPaid + {win}, TotalRounds = TotalRounds + {roundInc}, LastCalculated = {now} WHERE Id = {gStats.Id}");

        // Atomic update for User Stats
        _context.Database.ExecuteSqlInterpolated(
            $"UPDATE RTPStatistics SET TotalWagered = TotalWagered + {bet}, TotalPaid = TotalPaid + {win}, TotalRounds = TotalRounds + {roundInc}, LastCalculated = {now} WHERE Id = {uStats.Id}");
    }

    public IEnumerable<RTPStatistics> GetAllRtpStats() {
        return _context.RTPStatistics.ToList();
    }

    public void CleanupOldRtpStats(int daysToKeep) {
        var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
        // Only cleanup non-global and non-user aggregate stats if needed, 
        // but here we just delete anything older than cutoff to keep it lean.
        _context.Database.ExecuteSqlRaw("DELETE FROM RTPStatistics WHERE LastCalculated < {0}", cutoff);
    }

    public IEnumerable<Jackpot> GetJackpots() {
        var jackpots = _context.Jackpots.ToList();
        
        // Use FIXED GUIDs so Redis values persist even after DB resets
        var majorId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var megaId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var juiceId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var tournamentId = Guid.Parse("00000000-0000-0000-0000-000000000004");

        // FORCE RESET if values or IDs are wrong
        if (!jackpots.Any(j => j.Id == majorId) || jackpots.Any(j => j.CurrentValue < 1000 && j.Tier == JackpotTier.Major)) 
        {
            _context.Jackpots.RemoveRange(jackpots);
            _context.SaveChanges();
            jackpots = new List<Jackpot>();
        }

        if (!jackpots.Any()) {
            var cloverChaseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var fruitBlastId = Guid.Parse("44444444-4444-4444-4444-444444444444");

            _context.Jackpots.AddRange(
                new Jackpot { Id = majorId, Name = "Clover Major", Tier = JackpotTier.Major, CurrentValue = 2500, ContributionRate = 0.005m, IsGlobal = false, GameId = cloverChaseId, MustDropAt = null, LastUpdated = DateTime.UtcNow },
                new Jackpot { Id = megaId, Name = "Clover Mega", Tier = JackpotTier.Mega, CurrentValue = 10000, ContributionRate = 0.002m, IsGlobal = false, GameId = cloverChaseId, MustDropAt = null, LastUpdated = DateTime.UtcNow },
                new Jackpot { Id = juiceId, Name = "Juice Reservoir", Tier = JackpotTier.Special, CurrentValue = 1000, ContributionRate = 0.005m, IsGlobal = false, GameId = fruitBlastId, MustDropAt = null, LastUpdated = DateTime.UtcNow },
                new Jackpot { Id = tournamentId, Name = "Season Tournament", Tier = JackpotTier.Tournament, CurrentValue = 25000, ContributionRate = 0.001m, IsGlobal = true, MustDropAt = null, LastUpdated = DateTime.UtcNow }
            );
            _context.SaveChanges();
            return _context.Jackpots.ToList();
        }
        return jackpots;
    }

    public Jackpot GetGlobalJackpot() {
        return _context.Jackpots.FirstOrDefault(j => j.IsGlobal) ?? GetJackpots().First(j => j.IsGlobal);
    }

    public Jackpot GetOrCreateLocalJackpot(Guid gameId) {
        var jackpot = _context.Jackpots.FirstOrDefault(j => j.GameId == gameId && !j.IsGlobal);
        if (jackpot == null) {
            jackpot = new Jackpot {
                Id = Guid.NewGuid(),
                GameId = gameId,
                Name = "Local Jackpot",
                Tier = JackpotTier.Grand,
                CurrentValue = 1000m,
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

    public void LogAuditBatch(IEnumerable<AuditEvent> auditEvents) {
        if (!auditEvents.Any()) return;
        
        // PERFORMANCE: Still using a single SaveChanges for the batch
        // but adding one by one to better preserve order in the change tracker
        foreach (var ev in auditEvents) {
            _context.AuditLogs.Add(ev);
        }
        _context.SaveChanges();
    }

    public IEnumerable<AuditEvent> GetAuditLogs(int count) {
        return _context.AuditLogs.OrderByDescending(x => x.Timestamp).Take(count).ToList();
    }

    public IEnumerable<AuditEvent> GetAllAuditLogs() {
        return _context.AuditLogs.OrderBy(x => x.Timestamp).ToList();
    }

    public void CleanupOldAuditLogs(int daysToKeep) {
        var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
        _context.Database.ExecuteSqlRaw("DELETE FROM AuditLogs WHERE Timestamp < {0}", cutoff);
    }

    public string? GetLastAuditHash() {
        return _context.AuditLogs.OrderByDescending(x => x.Timestamp).Select(x => x.Hash).FirstOrDefault();
    }

    public AdminDashboardStats GetStatsForPeriod(DateTime start, DateTime end) {
        var roundsQuery = _context.GameRounds
            .Where(r => r.ExecutedAt >= start && r.ExecutedAt < end);

        var joinedQuery = roundsQuery
            .Join(_context.GameSessions, r => r.GameSessionId, s => s.Id, (r, s) => new { r, s })
            .Join(_context.Users, x => x.s.UserId, u => u.Id, (x, u) => new { x.r, x.s, u })
            .Where(x => !x.u.Username.StartsWith("Sim_") && x.u.Role != Role.Admin);

        var totals = joinedQuery
            .GroupBy(_ => 1)
            .Select(g => new {
                TotalBets = g.Sum(x => x.r.TotalBetAmount),
                TotalWins = g.Sum(x => x.r.TotalWinAmount)
            })
            .FirstOrDefault();

        var activeCount = joinedQuery.Select(x => x.u.Id).Distinct().Count();

        var gameStats = joinedQuery
            .Join(_context.Games, x => x.s.GameId, g => g.Id, (x, g) => new { x.r, g })
            .GroupBy(x => new { x.g.Name, x.g.Type })
            .Select(g => new GameStatDto {
                GameName = g.Key.Name,
                GameType = g.Key.Type,
                TotalWagered = g.Sum(x => x.r.TotalBetAmount),
                TotalWon = g.Sum(x => x.r.TotalWinAmount),
                MaxWin = g.Max(x => x.r.TotalWinAmount)
            })
            .ToList();

        var topPlayers = joinedQuery
            .GroupBy(x => x.u.Username)
            .Select(g => new PlayerRankDto {
                Username = g.Key,
                TotalWagered = g.Sum(x => x.r.TotalBetAmount),
                TotalWon = g.Sum(x => x.r.TotalWinAmount),
                Profit = g.Sum(x => x.r.TotalWinAmount - x.r.TotalBetAmount)
            })
            .OrderByDescending(x => x.TotalWon)
            .Take(10)
            .ToList();

        return new AdminDashboardStats {
            TotalBets = totals?.TotalBets ?? 0,
            TotalWins = totals?.TotalWins ?? 0,
            Ggr = (totals?.TotalBets ?? 0) - (totals?.TotalWins ?? 0),
            CurrentRtp = totals?.TotalBets > 0 ? (totals.TotalWins / totals.TotalBets) * 100 : 0,
            ActivePlayerCount = activeCount,
            GameStats = gameStats,
            TopPlayers = topPlayers,
            IsEmergencyStopActive = GetGlobalSetting("EmergencyStop") == "true"
        };
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

    public decimal GetMonthlyWagering(DateTime month) {
        var start = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);

        return _context.Bets
            .Where(b => b.CreatedAt >= start && b.CreatedAt < end)
            .Join(_context.Users, b => b.UserId, u => u.Id, (b, u) => new { b, u })
            .Where(x => !x.u.Username.StartsWith("Sim_") && x.u.Role != Role.Admin)
            .Sum(x => (decimal?)x.b.Amount) ?? 0m;
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
        return _context.Quests.Where(q => q.IsActive).ToList();
    }

    public Quest? GetQuest(Guid questId) {
        return _context.Quests.Find(questId);
    }

    public void CreateQuest(Quest quest) {
        _context.Quests.Add(quest);
        _context.SaveChanges();
    }

    public void UpdateQuest(Quest quest) {
        var existing = _context.Quests.Local.FirstOrDefault(q => q.Id == quest.Id);
        if (existing != null) {
            _context.Entry(existing).State = EntityState.Detached;
        }
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

    public IEnumerable<Achievement> GetAllAchievements() {
        return _context.Achievements.ToList();
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

    public int GetRoundCountByUser(Guid userId) {
        return _context.GameRounds.Count(r => _context.GameSessions.Any(s => s.Id == r.GameSessionId && s.UserId == userId));
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

    public IEnumerable<UserVoucher> GetVoucherUsages(Guid voucherId) {
        return _context.UserVouchers.Where(uv => uv.VoucherId == voucherId).ToList();
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

    public void CreateUserSession(UserSession session) {
        _context.UserSessions.Add(session);
        _context.SaveChanges();
    }

    public UserSession? GetUserSession(Guid sessionId) {
        return _context.UserSessions.FirstOrDefault(s => s.Id == sessionId);
    }

    public void UpdateUserSession(UserSession session) {
        _context.UserSessions.Update(session);
        _context.SaveChanges();
    }

    public List<UserSession> GetUserSessions(Guid userId) {
        return _context.UserSessions
            .Where(s => s.UserId == userId && s.IsActive)
            .OrderByDescending(s => s.LastActiveAt)
            .Take(10)
            .ToList();
    }

    public void InactivateSession(string refreshToken) {
        var sessions = _context.UserSessions.Where(s => s.RefreshToken == refreshToken).ToList();
        foreach (var s in sessions) {
            s.IsActive = false;
            // Immediate cache invalidation for security (Issue 42)
            _ = _redisCache.SetAsync($"session_active:{s.Id}", false, TimeSpan.FromMinutes(5));
        }
        _context.SaveChanges();
    }

    public void InactivateAllUserSessions(Guid userId) {
        var sessions = _context.UserSessions.Where(s => s.UserId == userId && s.IsActive).ToList();
        foreach (var s in sessions) {
            s.IsActive = false;
            // Immediate cache invalidation for security (Issue 42)
            _ = _redisCache.SetAsync($"session_active:{s.Id}", false, TimeSpan.FromMinutes(5));
        }
        _context.SaveChanges();
    }

    public void DeleteUserSession(Guid sessionId) {
        var session = _context.UserSessions.FirstOrDefault(s => s.Id == sessionId);
        if (session != null) {
            session.IsActive = false;
            _context.SaveChanges();
        }
    }

    public void SaveTransaction(Transaction transaction) {
        _context.Transactions.Add(transaction);
        _context.SaveChanges();
    }

    public Transaction? GetTransaction(Guid id) {
        return _context.Transactions.FirstOrDefault(t => t.Id == id);
    }

    public IEnumerable<Transaction> GetUserTransactions(Guid userId, int count) {
        return _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Timestamp)
            .Take(count)
            .ToList();
    }

    public string GetGlobalSetting(string key) {
        string cacheKey = $"setting:{key}";
        
        var value = _context.GlobalSettings.FirstOrDefault(s => s.Key == key)?.Value ?? string.Empty;
        
        if (!string.IsNullOrEmpty(value)) {
            _ = _redisCache.SetAsync(cacheKey, value, TimeSpan.FromMinutes(5));
        }
        
        return value;
    }

    public async Task<string> GetGlobalSettingAsync(string key) {
        string cacheKey = $"setting:{key}";
        var cachedValue = await _redisCache.GetAsync<string>(cacheKey);
        if (cachedValue != null) return cachedValue;

        var setting = await _context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
        var value = setting?.Value ?? string.Empty;
        
        if (!string.IsNullOrEmpty(value)) {
            await _redisCache.SetAsync(cacheKey, value, TimeSpan.FromMinutes(5));
        }
        
        return value;
    }

    public IEnumerable<GlobalSetting> GetAllGlobalSettings() {
        return _context.GlobalSettings.ToList();
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

        // PERFORMANCE: Sync to Redis immediately with 5 min TTL
        _ = _redisCache.SetAsync($"setting:{key}", value, TimeSpan.FromMinutes(5));
    }

    public void SaveSupportMessage(SupportMessage message) {
        _context.SupportMessages.Add(message);
        _context.SaveChanges();
    }

    public IEnumerable<SupportMessage> GetAllSupportMessages() {
        return _context.SupportMessages.OrderByDescending(m => m.CreatedAt).ToList();
    }

    public IEnumerable<SupportMessage> GetSupportMessages(int count) {
        return _context.SupportMessages.OrderByDescending(m => m.CreatedAt).Take(count).ToList();
    }

    public void UpdateSupportMessage(SupportMessage message) {
        _context.SupportMessages.Update(message);
        _context.SaveChanges();
    }

    public void MarkSupportMessageRead(Guid messageId) {
        var msg = _context.SupportMessages.Find(messageId);
        if (msg != null) {
            msg.IsRead = true;
            _context.SaveChanges();
        }
    }

    public IEnumerable<Tournament> GetAllTournaments() {
        return _context.Tournaments.OrderByDescending(t => t.StartDate).ToList();
    }

    public void CreateTournament(Tournament tournament) {
        _context.Tournaments.Add(tournament);
        _context.SaveChanges();
    }

    public void UpdateTournament(Tournament tournament) {
        _context.Tournaments.Update(tournament);
        _context.SaveChanges();
    }

    public void DeleteTournament(Guid id) {
        var t = _context.Tournaments.Find(id);
        if (t != null) {
            _context.Tournaments.Remove(t);
            _context.SaveChanges();
        }
    }

    public TournamentStatsDto GetTournamentStats(DateTime date) {
        var targetDate = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var entries = _context.TournamentEntries
            .Where(t => t.TournamentDate == targetDate)
            .ToList();

        decimal startingPool = 25000m;
        if (decimal.TryParse(GetGlobalSetting("TournamentPrizePool"), out var dbVal)) {
            startingPool = dbVal;
        }

        var jackpot = _context.Jackpots.FirstOrDefault(j => j.Tier == JackpotTier.Tournament);
        decimal currentPool = jackpot?.CurrentValue ?? startingPool;

        return new TournamentStatsDto {
            TotalParticipants = entries.Count,
            TotalWagered = entries.Sum(e => e.TotalWagered),
            TotalPayout = entries.Sum(e => e.TotalPayout),
            TotalRounds = entries.Sum(e => e.RoundCount),
            StartingPrizePool = startingPool,
            CurrentPrizePool = currentPool,
            EndDate = targetDate.AddMonths(1).AddSeconds(-1),
            ActiveSessions = _context.GameSessions.Count(s => s.IsActive && s.LastActivityAt > DateTime.UtcNow.AddMinutes(-5))
        };
    }

    public IEnumerable<SystemError> GetRecentErrors(int count) {
        return _context.SystemErrors.OrderByDescending(e => e.CreatedAt).Take(count).ToList();
    }

    public void ClearAllErrors() {
        _context.SystemErrors.RemoveRange(_context.SystemErrors);
        _context.SaveChanges();
    }

    public void SaveChatMessage(ChatMessage message) {
        _context.ChatMessages.Add(message);
        _context.SaveChanges();
    }

    public ChatMessage? GetChatMessage(Guid messageId) {
        return _context.ChatMessages.Find(messageId);
    }

    public void UpdateChatMessage(ChatMessage message) {
        _context.ChatMessages.Update(message);
        _context.SaveChanges();
    }

    public void MarkPrivateMessagesAsRead(Guid senderId, Guid receiverId) {
        var unread = _context.ChatMessages
            .Where(m => m.SenderId == senderId && m.ReceiverId == receiverId && m.Type == ChatMessageType.Private && !m.IsRead)
            .ToList();
            
        if (unread.Any()) {
            foreach(var msg in unread) msg.IsRead = true;
            _context.SaveChanges();
        }
    }

    public IEnumerable<ChatMessage> GetGlobalChatMessages(int count) {
        return _context.ChatMessages
            .Where(m => m.Type == ChatMessageType.Global)
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .ToList()
            .OrderBy(m => m.Timestamp); // Re-order in memory
    }

    public IEnumerable<ChatMessage> GetPrivateChatHistory(Guid userId1, Guid userId2, int count) {
        // Query both directions separately to ensure simple SQL translation
        var msgs = _context.ChatMessages
            .Where(m => m.Type == ChatMessageType.Private && 
                       ((m.SenderId == userId1 && m.ReceiverId == userId2) || 
                        (m.SenderId == userId2 && m.ReceiverId == userId1)))
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .ToList();
            
        return msgs.OrderBy(m => m.Timestamp);
    }

    public IEnumerable<(Guid Id, string Username, string AvatarUrl)> GetRecentPrivateInterlocutors(Guid userId) {
        // Separate queries to avoid Union/Distinct translation issues
        var sentToIds = _context.ChatMessages
            .Where(m => m.SenderId == userId && m.Type == ChatMessageType.Private && m.ReceiverId != null)
            .Select(m => m.ReceiverId!.Value)
            .Distinct()
            .ToList();

        var receivedFromIds = _context.ChatMessages
            .Where(m => m.ReceiverId == userId && m.Type == ChatMessageType.Private)
            .Select(m => m.SenderId)
            .Distinct()
            .ToList();

        var allIds = sentToIds.Concat(receivedFromIds).Distinct().ToList();

        return _context.Users
            .Where(u => allIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, u.AvatarUrl })
            .ToList()
            .Select(u => (u.Id, u.Username, u.AvatarUrl))
            .ToList();
    }

    public void DeleteUser(Guid userId) {
        var user = _context.Users.Find(userId);
        if (user != null) {
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

    public IRedisCacheService GetRedisCache() => _redisCache;
}