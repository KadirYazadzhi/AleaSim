using AleaSim.Domain.Models;

namespace AleaSim.Domain.Interfaces;

public interface IBrainService {
    Task<BrainDirective> DecideOutcomeAsync(Guid userId, Guid gameId, decimal betAmount, IGameRepository repo, bool isShadowMode = false);
    Task<BrainDirective> GetNextDirectiveAsync(Guid userId, Guid gameId, decimal betAmount, IGameRepository repo);
    Task UpdateProfileAsync(Guid userId, decimal betAmount, decimal winAmount, IGameRepository repo);
    Task SyncProfileToCacheAsync(Guid userId, IGameRepository repo);
    void SetForcedDirective(Guid userId, BrainDirective directive);
}