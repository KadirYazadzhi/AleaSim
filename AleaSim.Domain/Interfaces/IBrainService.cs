using AleaSim.Domain.Models;

namespace AleaSim.Domain.Interfaces;

public interface IBrainService {
    BrainDirective DecideOutcome(Guid userId, Guid gameId, decimal betAmount, IGameRepository repo, bool isShadowMode = false);
    BrainDirective GetNextDirective(Guid userId, Guid gameId, decimal betAmount, IGameRepository repo); 
    void UpdateProfile(Guid userId, decimal betAmount, decimal winAmount, IGameRepository? repo = null); 
    void SetForcedDirective(Guid userId, BrainDirective directive);
}