using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Interfaces;

public interface IQuestService {
    IEnumerable<Quest> GetActiveQuests(Guid userId, IGameRepository repo);
    void GenerateDailyQuests(Guid userId, IGameRepository repo);
    
    // Updated to Async to support Vault operations
    Task UpdateProgressAsync(Guid userId, string type, int amount, IGameRepository repo, IVaultService vault);
    Task<bool> ClaimRewardAsync(Guid questId, IGameRepository repo, IVaultService vault);
}