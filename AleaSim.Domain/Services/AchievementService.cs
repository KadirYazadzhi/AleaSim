using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class AchievementService : IAchievementService {
    private readonly IRealTimeService _realTimeService;

    public AchievementService(IRealTimeService realTimeService) {
        _realTimeService = realTimeService;
    }

    public async Task CheckAchievements(Guid userId, string conditionType, decimal currentValue, IGameRepository repo) {
        // 1. Get all achievements for this condition type
        // Note: Repo needs GetAchievementsByCondition
        var potential = repo.GetAchievementsByCondition(conditionType);
        
        // 2. Get already unlocked for this user
        var unlockedIds = repo.GetUserAchievements(userId).Select(a => a.AchievementId).ToHashSet();

        foreach (var ach in potential) {
            if (unlockedIds.Contains(ach.Id)) continue;

            if (currentValue >= ach.ConditionValue) {
                // UNLOCK!
                var ua = new UserAchievement {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    AchievementId = ach.Id,
                    UnlockedAt = DateTime.UtcNow
                };
                repo.SaveUserAchievement(ua);

                // Notify User
                await _realTimeService.NotifyGameUpdate(userId, new {
                    Type = "AchievementUnlocked",
                    Name = ach.Name,
                    Description = ach.Description,
                    Icon = ach.Icon,
                    Message = $"🏆 Achievement Unlocked: {ach.Name}!"
                });
            }
        }
    }

    public async Task<IEnumerable<UserAchievement>> GetUserAchievements(Guid userId, IGameRepository repo) {
        return repo.GetUserAchievements(userId);
    }
}
