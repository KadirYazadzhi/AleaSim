using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IQuestService {
    IEnumerable<Quest> GetActiveQuests(Guid userId, IGameRepository repo);
    void GenerateDailyQuests(Guid userId, IGameRepository repo);
    void UpdateProgress(Guid userId, string type, int amount, IGameRepository repo, IVaultService vault);
    bool ClaimReward(Guid questId, IGameRepository repo, IVaultService vault);
}
