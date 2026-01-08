using AleaSim.Shared.Models;
using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IGameRepository {
    // Transaction Support
    ITransaction BeginTransaction();
    void SaveChanges();

    // Sessions & Users
    GameSession CreateSession(GameSession session);
    GameSession? GetSession(Guid sessionId);
    IEnumerable<GameSession> GetAllActiveSessions(); // Added
    IEnumerable<ActiveSessionDto> GetActiveSessionsDetails();
    void EndSession(Guid sessionId);
    
    // Games
    Game? GetGame(Guid gameId);
    Game? GetGameByType(string gameType);
    void CreateGame(Game game);
    void UpdateGame(Game game);

    User? GetUser(Guid userId);
    User? GetUserBySessionId(Guid sessionId);
    User? GetUserByUsername(string username);
    IEnumerable<User> SearchUsers(string query);
    void CreateUser(User user);
    void UpdateUser(User user); // Added generic update
    void UpdateUserBalance(Guid userId, decimal amountToAdd); // Positive to add, negative to subtract
    IEnumerable<User> GetUsersWithExpiredBonuses(DateTime cutoff);

    // Player Profile
    PlayerProfile? GetPlayerProfile(Guid userId);
    IEnumerable<PlayerProfile> GetActiveProfiles(TimeSpan activityWindow);
    void CreatePlayerProfile(PlayerProfile profile);
    void UpdatePlayerProfile(PlayerProfile profile);

    // Tournament
    TournamentEntry GetOrCreateTournamentEntry(Guid userId, DateTime date);
    void UpdateTournamentEntry(TournamentEntry entry);
    IEnumerable<TournamentEntry> GetTopTournamentEntries(DateTime date, int topCount);

    // Analytics
    IEnumerable<(Guid UserId, decimal NetResult)> CalculateDailyNet(DateTime date);

    // Bets
    void SaveBet(Bet bet);
    void UpdateBet(Bet bet); // Added to update Round ID
    Bet? GetLastBet(Guid sessionId);

    // Rounds
    int GetRoundCount(Guid sessionId);
    void SaveRound(GameRound round);
    GameRound? GetLastRound(Guid sessionId);
    IEnumerable<GameRound> GetUserRounds(Guid userId, int count);
    IEnumerable<GameRound> GetGlobalRecentRounds(int count);
    IEnumerable<GameRoundDto> GetUserHistory(Guid userId, int count);

    // Outcomes
    void SaveOutcome(Outcome outcome);
    Outcome? GetOutcome(Guid roundId);

    // RTP
    RTPStatistics GetOrCreateGameStats(Guid gameId);
    RTPStatistics GetOrCreateUserStats(Guid userId);
    void UpdateRtpStats(Guid gameId, Guid userId, decimal bet, decimal win);
    IEnumerable<RTPStatistics> GetAllRtpStats(); // Optional for aggregation

    // Jackpots
    IEnumerable<Jackpot> GetJackpots(); // Added
    Jackpot GetGlobalJackpot();
    Jackpot GetOrCreateLocalJackpot(Guid gameId);
    void UpdateJackpot(Jackpot jackpot);
    void SaveJackpotTrigger(Jackpot jackpot, decimal winAmount); // Handle reset logic inside implementation or just save state

    // Audit
    void LogAudit(AuditEvent auditEvent);
    IEnumerable<AuditEvent> GetAuditLogs(int count);
    IEnumerable<AuditEvent> GetAllAuditLogs();
    string? GetLastAuditHash();

    // Admin Reporting
    (decimal TotalBets, decimal TotalWins) GetDailyFinancials(DateTime date);
    IEnumerable<(DateTime Hour, decimal Bets, decimal Wins)> GetRtpTrend(int hours);
    int GetActivePlayerCount(int minutes);
    IEnumerable<(string Username, decimal TotalWin)> GetTopWinners(DateTime date, int topCount);

    // Quests
    IEnumerable<Quest> GetActiveQuests(Guid userId);
    Quest? GetQuest(Guid questId);
    void CreateQuest(Quest quest);
    void UpdateQuest(Quest quest);

    // RPG Progression
    UserProgression? GetUserProgression(Guid userId);
    void CreateUserProgression(UserProgression progression);
    void UpdateUserProgression(UserProgression progression);

    // Achievements
    IEnumerable<Achievement> GetAchievementsByCondition(string conditionType);
    IEnumerable<UserAchievement> GetUserAchievements(Guid userId);
    void SaveUserAchievement(UserAchievement userAchievement);

    // Vouchers
    Voucher? GetVoucherByCode(string code);
    bool HasUserRedeemedVoucher(Guid userId, Guid voucherId);
    void UpdateVoucher(Voucher voucher);
    void SaveUserVoucher(UserVoucher userVoucher);
    void CreateVoucher(Voucher voucher);
    IEnumerable<Voucher> GetAllVouchers();

    // Transactions
    void SaveTransaction(Transaction transaction);
    IEnumerable<Transaction> GetUserTransactions(Guid userId, int count);

    // Tournament History
    void SaveTournamentWinners(IEnumerable<TournamentWinner> winners);
    IEnumerable<TournamentWinner> GetTournamentHistory(int months);

    // Global Settings
    string GetGlobalSetting(string key);
    void SetGlobalSetting(string key, string value, string description = "");
}
