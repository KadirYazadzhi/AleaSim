using AleaSim.Domain.Models;

namespace AleaSim.Domain.Interfaces;

public interface IBrainService {
    BrainDirective DecideOutcome(Guid userId, Guid gameId, decimal betAmount, bool isShadowMode = false);
    void UpdateProfile(Guid userId, decimal betAmount, decimal winAmount);
}
