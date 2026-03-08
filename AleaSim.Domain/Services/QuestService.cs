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
        // Return only the top 3 active quests that are NOT completed.
        // This creates the "infinite" quest loop feeling.
        var allQuests = repo.GetAllQuests().Where(q => q.IsActive).ToList();
        var userProgs = repo.GetUserQuestProgressions(userId).ToDictionary(p => p.QuestId);

        var activeResults = new List<UserQuestProgress>();
        
        // 1. Get quests already in progress but not completed
        foreach (var q in allQuests) {
            if (userProgs.TryGetValue(q.Id, out var prog)) {
                if (!prog.IsCompleted) {
                    prog.Quest = q;
                    activeResults.Add(prog);
                }
            }
        }

        // 2. If we have less than 3, fill from the pool of quests the user hasn't started or completed yet
        if (activeResults.Count < 3) {
            var availablePool = allQuests.Where(q => !userProgs.ContainsKey(q.Id)).ToList();
            foreach (var q in availablePool.Take(3 - activeResults.Count)) {
                activeResults.Add(new UserQuestProgress { 
                    QuestId = q.Id, 
                    Quest = q, 
                    UserId = userId, 
                    CurrentValue = 0,
                    IsCompleted = false
                });
            }
        }

        return await Task.FromResult(activeResults.Take(3));
    }
}
