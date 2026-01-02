using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface ILevelService {
    void AddExperience(Guid userId, decimal betAmount, IGameRepository repo, IRealTimeService realTime);
    UserProgression GetProgression(Guid userId, IGameRepository repo);
}
