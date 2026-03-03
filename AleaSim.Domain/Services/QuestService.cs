using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class QuestService : IQuestService {
    public async Task UpdateProgressAsync(Guid userId, string goalType, decimal value, IGameRepository repo, IRealTimeService realTime, IVaultService vault) {
        var activeQuests = repo.GetAllQuests().Where(q => q.IsActive && q.GoalType == goalType).ToList();
        var userProgressions = repo.GetUserQuestProgressions(userId);

        foreach (var quest in activeQuests) {
            var progress = userProgressions.FirstOrDefault(p => p.QuestId == quest.Id);
            
            if (progress == null) {
                progress = new UserQuestProgress {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    QuestId = quest.Id,
                    CurrentValue = 0,
                    IsCompleted = false
                };
                repo.CreateUserQuestProgress(progress);
            }

            if (progress.IsCompleted) continue;

            progress.CurrentValue += value;

            if (progress.CurrentValue >= quest.TargetValue) {
                progress.CurrentValue = quest.TargetValue;
                progress.IsCompleted = true;
                progress.CompletedAt = DateTime.UtcNow;

                // Award Reward
                await vault.CreditBonusAsync(userId, quest.RewardAmount, quest.RewardAmount * 5, repo);

                // Notify User
                await realTime.NotifyGameUpdate(userId, new {
                    Type = "QuestCompleted",
                    Title = quest.Title,
                    Reward = quest.RewardAmount,
                    Message = $"Mission Accomplished: {quest.Title}! Rewarded {quest.RewardAmount:C}."
                });
            }

            repo.UpdateUserQuestProgress(progress);
        }
    }

    public async Task GenerateDailyQuests(Guid userId, IGameRepository repo) {
        // Implementation for generating daily quests if needed, 
        // for now we rely on seeded global quests.
        await Task.CompletedTask;
    }

    public async Task<IEnumerable<UserQuestProgress>> GetActiveQuests(Guid userId, IGameRepository repo) {
        // Return all active quests with their user progress (or empty progress if new)
        var allActive = repo.GetAllQuests().Where(q => q.IsActive).ToList();
        var userProgs = repo.GetUserQuestProgressions(userId).ToDictionary(p => p.QuestId);

        var results = new List<UserQuestProgress>();
        foreach (var q in allActive) {
            if (userProgs.TryGetValue(q.Id, out var prog)) {
                prog.Quest = q; // Ensure navigation property is set for DTO
                results.Add(prog);
            } else {
                results.Add(new UserQuestProgress { QuestId = q.Id, Quest = q, UserId = userId, CurrentValue = 0 });
            }
        }
        return await Task.FromResult(results);
    }
}
