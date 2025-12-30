using AleaSim.Domain.Models;

namespace AleaSim.Domain.Interfaces;

public interface IBrainService {
    /// <summary>
    /// Analyzes the user's context and determines the outcome of the next spin.
    /// </summary>
    BrainDirective DecideOutcome(Guid userId, Guid gameId, decimal betAmount);

    /// <summary>
    /// Updates the player's profile after a round is completed.
    /// </summary>
    void UpdateProfile(Guid userId, decimal betAmount, decimal winAmount);
}
