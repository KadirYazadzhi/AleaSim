using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public interface IAchievementService {
    Task CheckAchievements(Guid userId, IGameRepository repo, IRealTimeService realTime);
}

public class AchievementService : IAchievementService {
    public async Task CheckAchievements(Guid userId, IGameRepository repo, IRealTimeService realTime) {
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
                var ua = new UserAchievement {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    AchievementId = ach.Id,
                    UnlockedAt = DateTime.UtcNow
                };
                repo.SaveUserAchievement(ua);
                
                // Notify user
                await realTime.NotifyGameUpdate(userId, new { 
                    Action = "AchievementUnlocked", 
                    Name = ach.Name, 
                    Icon = ach.Icon,
                    Description = ach.Description 
                });
            }
        }
    }
}
