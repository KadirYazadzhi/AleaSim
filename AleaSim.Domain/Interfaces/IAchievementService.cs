using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IAchievementService {
    Task CheckAchievements(Guid userId, string conditionType, decimal currentValue, IGameRepository repo);
    Task CheckAchievements(Guid userId, IGameRepository repo, IRealTimeService realTime);
    Task<IEnumerable<UserAchievement>> GetUserAchievements(Guid userId, IGameRepository repo);
}
