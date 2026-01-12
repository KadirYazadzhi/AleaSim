using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface ILevelService {
    Task AddExperience(Guid userId, decimal betAmount, IGameRepository repo, IRealTimeService realTime);
    UserProgression GetProgression(Guid userId, IGameRepository repo);
    Task<bool> UpgradeSkill(Guid userId, string skillName, IGameRepository repo);
}