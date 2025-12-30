using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IVaultService {
    /// <summary>
    /// Deducts the bet amount from the appropriate wallet (Bonus first, then Real).
    /// Returns true if funds are sufficient.
    /// </summary>
    bool ProcessBet(Guid userId, decimal amount, IGameRepository repo);

    /// <summary>
    /// Credits the win amount to the appropriate wallet.
    /// Updates Wagering Progress if Bonus is active.
    /// </summary>
    void ProcessWin(Guid userId, decimal amount, IGameRepository repo);

    /// <summary>
    /// Checks if the casino (Pool) can afford to pay this win.
    /// Also checks User's pRTP status (Shadow Wallet).
    /// </summary>
    bool CanAffordWin(Guid userId, Guid gameId, decimal winAmount, IGameRepository repo);

    /// <summary>
    /// Adds funds to the user's bonus wallet (e.g., from Raffle).
    /// </summary>
    void CreditBonus(Guid userId, decimal amount, decimal wageringRequirement, IGameRepository repo);
}
