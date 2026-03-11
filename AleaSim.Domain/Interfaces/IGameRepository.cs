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
    void UpdateSession(GameSession session);
    IEnumerable<GameSession> GetAllActiveSessions(); // Added
    IEnumerable<ActiveSessionDto> GetActiveSessionsDetails();
    void EndSession(Guid sessionId);
    
    // Games
    Game? GetGame(Guid gameId);
    Game? GetGameByType(string gameType);
    void CreateGame(Game game);
    void UpdateGame(Game game);
    void UpdateGamePoolBalance(Guid gameId, decimal amountToAdd);

    User? GetUser(Guid userId);
    User? GetUserBySessionId(Guid sessionId);
    User? GetUserByUsername(string username);
    IEnumerable<User> SearchUsers(string query);
    IEnumerable<User> GetAllUsers();
    int GetTotalUserCount();
    void CreateUser(User user);
    void UpdateUser(User user); // Added generic update
    void DeleteUser(Guid userId);
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
    decimal GetUserDailyLoss(Guid userId, DateTime date);

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
    IEnumerable<GameRoundDto> GetGlobalHistory(int count);

    // Outcomes
    void SaveOutcome(Outcome outcome);
    Outcome? GetOutcome(Guid roundId);

    // RTP
    RTPStatistics GetOrCreateGameStats(Guid gameId);
    RTPStatistics GetOrCreateUserStats(Guid userId);
        void UpdateRtpStats(Guid gameId, Guid userId, decimal bet, decimal win);
        IEnumerable<RTPStatistics> GetAllRtpStats();
        void CleanupOldRtpStats(int daysToKeep);
     // Optional for aggregation

    // Jackpots
    IEnumerable<Jackpot> GetJackpots(); // Added
    Jackpot GetGlobalJackpot();
    Jackpot GetOrCreateLocalJackpot(Guid gameId);
    void UpdateJackpot(Jackpot jackpot);
    void SaveJackpotTrigger(Jackpot jackpot, decimal winAmount); // Handle reset logic inside implementation or just save state

    // Audit
    void LogAudit(AuditEvent auditEvent);
    void LogAuditBatch(IEnumerable<AuditEvent> auditEvents);
    IEnumerable<AuditEvent> GetAuditLogs(int count);
    IEnumerable<AuditEvent> GetAllAuditLogs();
    void CleanupOldAuditLogs(int daysToKeep);
    string? GetLastAuditHash();

    // Admin Reporting
    (decimal TotalBets, decimal TotalWins) GetDailyFinancials(DateTime date);
    decimal GetMonthlyWagering(DateTime month);
    decimal GetGlobalTotalRewardsPaid();
    IEnumerable<(DateTime Hour, decimal Bets, decimal Wins)> GetRtpTrend(int hours);
    int GetActivePlayerCount(int minutes);
    IEnumerable<(string Username, decimal TotalWin)> GetTopWinners(DateTime date, int topCount);

    // Quests
    IEnumerable<Quest> GetActiveQuests(Guid userId);
    IEnumerable<Quest> GetAllQuests(); // Added
    IEnumerable<UserQuestProgress> GetUserQuestProgressions(Guid userId); // Added
    void CreateUserQuestProgress(UserQuestProgress progress); // Added
    void UpdateUserQuestProgress(UserQuestProgress progress); // Added
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

    // User Sessions
    void CreateUserSession(UserSession session);
    UserSession? GetUserSession(Guid sessionId);
    List<UserSession> GetUserSessions(Guid userId);
    void InactivateSession(string refreshToken);
    void InactivateAllUserSessions(Guid userId);
    void DeleteUserSession(Guid sessionId);

    // Transactions
    void SaveTransaction(Transaction transaction);
    IEnumerable<Transaction> GetUserTransactions(Guid userId, int count);

    // Tournament History
    void SaveTournamentWinners(IEnumerable<TournamentWinner> winners);
    IEnumerable<TournamentWinner> GetTournamentHistory(int months);

    // Global Settings
    string GetGlobalSetting(string key);
    IEnumerable<GlobalSetting> GetAllGlobalSettings();
    void SetGlobalSetting(string key, string value, string description = "");

    // Support Messages
    void SaveSupportMessage(SupportMessage message);
    IEnumerable<SupportMessage> GetSupportMessages(int count);
    void MarkSupportMessageRead(Guid messageId);

    // Chat
    void SaveChatMessage(ChatMessage message);
    IEnumerable<ChatMessage> GetGlobalChatMessages(int count);
    IEnumerable<ChatMessage> GetPrivateChatHistory(Guid userId1, Guid userId2, int count);
    
    // Infrastructure
    AleaSim.Domain.Services.IRedisCacheService GetRedisCache();
}
