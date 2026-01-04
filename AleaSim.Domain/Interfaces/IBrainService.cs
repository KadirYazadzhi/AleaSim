using AleaSim.Domain.Models;

namespace AleaSim.Domain.Interfaces;

public interface IBrainService {
    BrainDirective DecideOutcome(Guid userId, Guid gameId, decimal betAmount, IGameRepository repo, bool isShadowMode = false);
    BrainDirective GetNextDirective(Guid userId, Guid gameId, decimal betAmount, IGameRepository repo); // Added
    void UpdateProfile(Guid userId, decimal betAmount, decimal winAmount); 
    void SetForcedDirective(Guid userId, BrainDirective directive);
}
