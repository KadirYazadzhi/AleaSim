using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IGameRepository {
    // Transaction Support
    ITransaction BeginTransaction();
    void SaveChanges();

    // Sessions & Users
    GameSession CreateSession(GameSession session);
    GameSession? GetSession(Guid sessionId);
    void EndSession(Guid sessionId);
    User? GetUser(Guid userId);
    User? GetUserBySessionId(Guid sessionId);
    User? GetUserByUsername(string username);
    void CreateUser(User user);
    void UpdateUser(User user); // Added generic update
    void UpdateUserBalance(Guid userId, decimal amountToAdd); // Positive to add, negative to subtract

    // Player Profile
    PlayerProfile? GetPlayerProfile(Guid userId);
    IEnumerable<PlayerProfile> GetActiveProfiles(TimeSpan activityWindow);
    void CreatePlayerProfile(PlayerProfile profile);
    void UpdatePlayerProfile(PlayerProfile profile);

    // Tournament
    TournamentEntry GetOrCreateTournamentEntry(Guid userId, DateTime date);
    void UpdateTournamentEntry(TournamentEntry entry);
    IEnumerable<TournamentEntry> GetTopTournamentEntries(DateTime date, int topCount);

    // Bets
    void SaveBet(Bet bet);
    void UpdateBet(Bet bet); // Added to update Round ID
    Bet? GetLastBet(Guid sessionId);

    // Rounds
    int GetRoundCount(Guid sessionId);
    void SaveRound(GameRound round);
    GameRound? GetLastRound(Guid sessionId);

    // Outcomes
    void SaveOutcome(Outcome outcome);
    Outcome? GetOutcome(Guid roundId);

    // RTP
    RTPStatistics GetOrCreateGameStats(Guid gameId);
    RTPStatistics GetOrCreateUserStats(Guid userId);
    void UpdateRtpStats(Guid gameId, Guid userId, decimal bet, decimal win);
    IEnumerable<RTPStatistics> GetAllRtpStats(); // Optional for aggregation

    // Jackpots
    Jackpot GetGlobalJackpot();
    Jackpot GetOrCreateLocalJackpot(Guid gameId);
    void UpdateJackpot(Jackpot jackpot);
    void SaveJackpotTrigger(Jackpot jackpot, decimal winAmount); // Handle reset logic inside implementation or just save state

    // Audit
    void LogAudit(AuditEvent auditEvent);
    IEnumerable<AuditEvent> GetAuditLogs(int count);
    string? GetLastAuditHash();
    
    // Game Lookups
    Game? GetGame(Guid gameId); // Added for RtpEngine
    void UpdateGame(Game game); // Added for RtpEngine
    Game? GetGameByType(string type);
    Game CreateGame(Game game);
}
