using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class QuestService : IQuestService {
    
    public IEnumerable<Quest> GetActiveQuests(Guid userId, IGameRepository repo) {
        return repo.GetActiveQuests(userId);
    }

    public void GenerateDailyQuests(Guid userId, IGameRepository repo) {
        var existing = repo.GetActiveQuests(userId).Any(q => q.CreatedAt.Date == DateTime.UtcNow.Date);
        if (existing) return;

        var quests = new List<Quest> {
            new Quest {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = "SpinCount",
                Description = "Spin 50 times today",
                TargetValue = 50,
                CurrentProgress = 0,
                RewardAmount = 10,
                Status = QuestStatus.Active,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            },
            new Quest {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = "WinAmount",
                Description = "Win a total of $100",
                TargetValue = 100,
                CurrentProgress = 0,
                RewardAmount = 25,
                Status = QuestStatus.Active,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            }
        };

        foreach (var q in quests) {
            repo.CreateQuest(q);
        }
    }

    public async Task UpdateProgressAsync(Guid userId, string type, int amount, IGameRepository repo, IVaultService vault) {
        var quests = repo.GetActiveQuests(userId).Where(q => q.Type == type && q.Status == QuestStatus.Active).ToList();
        
        foreach (var quest in quests) {
            quest.CurrentProgress += amount;
            
            if (quest.CurrentProgress >= quest.TargetValue) {
                quest.CurrentProgress = quest.TargetValue;
                quest.Status = QuestStatus.Completed;
                
                await ClaimRewardAsync(quest.Id, repo, vault);
            }
            
            repo.UpdateQuest(quest);
        }
    }

    public async Task<bool> ClaimRewardAsync(Guid questId, IGameRepository repo, IVaultService vault) {
        var quest = repo.GetQuest(questId);
        if (quest == null || (quest.Status != QuestStatus.Completed && quest.Status != QuestStatus.Active)) return false; 
        
        if (quest.Status == QuestStatus.Completed) {
            quest.Status = QuestStatus.Claimed;
            await vault.CreditBonusAsync(quest.UserId, quest.RewardAmount, quest.RewardAmount * 5, repo);
            repo.UpdateQuest(quest);
            return true;
        }
        return false;
    }
}