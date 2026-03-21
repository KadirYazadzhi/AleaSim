using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class AchievementService : IAchievementService {
    private readonly IRealTimeService _realTime;

    public AchievementService(IRealTimeService realTime) {
        _realTime = realTime;
    }

    public async Task CheckAchievements(Guid userId, string conditionType, decimal currentValue, IGameRepository repo) {
        // Implementation for single condition check (event-based)
        var allAchievements = repo.GetAchievementsByCondition(conditionType);
        var unlocked = repo.GetUserAchievements(userId).Select(a => a.AchievementId).ToHashSet();

        foreach (var ach in allAchievements) {
            if (unlocked.Contains(ach.Id) || currentValue < ach.ConditionValue) continue;

            await Unlock(userId, ach, repo);
        }
    }

    public async Task CheckAchievements(Guid userId, IGameRepository repo, IRealTimeService realTime) {
        // Global check (all conditions)
        var profile = repo.GetPlayerProfile(userId);
        if (profile == null) return;

        var unlocked = repo.GetUserAchievements(userId).Select(a => a.AchievementId).ToHashSet();
        var allAchievements = repo.GetAllAchievements();

        foreach (var ach in allAchievements) {
            if (unlocked.Contains(ach.Id)) continue;

            bool isMet = ach.ConditionType switch {
                "TotalWagered" => profile.TotalWagered >= ach.ConditionValue,
                "TotalPaid" => profile.TotalPaid >= ach.ConditionValue,
                "LevelReached" => (repo.GetUserProgression(userId)?.CurrentLevel ?? 1) >= ach.ConditionValue,
                "TotalBets" => repo.GetRoundCountByUser(userId) >= ach.ConditionValue,
                _ => false
            };

            if (isMet) {
                await Unlock(userId, ach, repo);
            }
        }
    }

    public async Task<IEnumerable<UserAchievement>> GetUserAchievements(Guid userId, IGameRepository repo) {
        return await Task.FromResult(repo.GetUserAchievements(userId));
    }

    private async Task Unlock(Guid userId, Achievement ach, IGameRepository repo) {
        var ua = new UserAchievement {
            Id = Guid.NewGuid(),
            UserId = userId,
            AchievementId = ach.Id,
            UnlockedAt = DateTime.UtcNow
        };
        repo.SaveUserAchievement(ua);
        
        // Notify user via injected realTime or passed one
        await _realTime.NotifyGameUpdate(userId, new { 
            Action = "AchievementUnlocked", 
            Name = ach.Name, 
            Icon = ach.Icon,
            Description = ach.Description 
        });
    }
}
