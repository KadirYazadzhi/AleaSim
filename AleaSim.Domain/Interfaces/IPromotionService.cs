namespace AleaSim.Domain.Interfaces;

public interface IPromotionService {
    /// <summary>
    /// Processes a bet to award raffle tickets and update tournament stats.
    /// </summary>
    void ProcessBetActivity(Guid userId, decimal betAmount, IGameRepository repo);

    /// <summary>
    /// Updates tournament stats with win amount.
    /// </summary>
    void ProcessWinActivity(Guid userId, decimal winAmount, IGameRepository repo);

    /// <summary>
    /// Executes a raffle draw for a specific prize amount.
    /// </summary>
    Task ExecuteRaffleDraw(decimal prizeAmount, string raffleType, IGameRepository repo);

    /// <summary>
    /// Checks if user is eligible for a drop (Active in last 3 mins).
    /// </summary>
    bool IsUserActive(Guid userId, IGameRepository repo);
}
