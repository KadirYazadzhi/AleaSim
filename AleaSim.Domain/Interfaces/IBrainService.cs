using AleaSim.Domain.Models;

namespace AleaSim.Domain.Interfaces;

public interface IBrainService {
    BrainDirective DecideOutcome(Guid userId, Guid gameId, decimal betAmount, IGameRepository repo, bool isShadowMode = false);
    void UpdateProfile(Guid userId, decimal betAmount, decimal winAmount); // Added
    void SetForcedDirective(Guid userId, BrainDirective directive);
}
