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
    User? GetUserByUsername(string username);
    void CreateUser(User user);
    void UpdateUserBalance(Guid userId, decimal amountToAdd); // Positive to add, negative to subtract

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
    Game? GetGameByType(string type);
    Game CreateGame(Game game);
}
