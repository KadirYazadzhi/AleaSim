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

    bool IsUserActive(Guid userId, IGameRepository repo);
    Task ExecuteRaffleDraw(decimal prizeAmount, string raffleType, IGameRepository repo);
    
    // Daily Reward
    Task<object> SpinBonusWheel(Guid userId, IGameRepository repo);
    Task<object> ClaimDailyStreakReward(Guid userId, IGameRepository repo);
}
