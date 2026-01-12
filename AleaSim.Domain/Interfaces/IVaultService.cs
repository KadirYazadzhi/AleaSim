using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IVaultService {
    /// <summary>
    /// Deducts the bet amount from the appropriate wallet (Bonus first, then Real).
    /// Returns true if funds are sufficient.
    /// </summary>
    Task<bool> ProcessBetAsync(Guid userId, decimal amount, IGameRepository repo);

    /// <summary>
    /// Credits the win amount to the appropriate wallet.
    /// Updates Wagering Progress if Bonus is active.
    /// </summary>
    Task ProcessWinAsync(Guid userId, decimal amount, IGameRepository repo);

    /// <summary>
    /// Checks if the casino (Pool) can afford to pay this win.
    /// Also checks User's pRTP status (Shadow Wallet).
    /// </summary>
    Task<bool> CanAffordWinAsync(Guid userId, Guid gameId, decimal winAmount, IGameRepository repo, bool strictShadowCheck = true);

    /// <summary>
    /// Synchronous check for AI/Brain logic. Does not lock. Not transactional.
    /// </summary>
    bool CanAffordWinCheck(Guid userId, Guid gameId, decimal winAmount, IGameRepository repo, bool strictShadowCheck = true);

    /// <summary>
    /// Adds funds to the user's bonus wallet (e.g., from Raffle).
    /// </summary>
    Task CreditBonusAsync(Guid userId, decimal amount, decimal wageringRequirement, IGameRepository repo);

    /// <summary>
    /// Converts Bonus to Real Balance at 10% rate if > 100. Else forfeits.
    /// </summary>
    Task<bool> CashoutBonusAsync(Guid userId, IGameRepository repo);
}