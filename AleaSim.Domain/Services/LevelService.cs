using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class LevelService : ILevelService {
    private const decimal XP_PER_CURRENCY_UNIT = 10m; // Bet $1 -> 10 XP

    public UserProgression GetProgression(Guid userId, IGameRepository repo) {
        return repo.GetUserProgression(userId) ?? CreateDefault(userId, repo);
    }

    private UserProgression CreateDefault(Guid userId, IGameRepository repo) {
        var prog = new UserProgression { Id = Guid.NewGuid(), UserId = userId };
        repo.CreateUserProgression(prog);
        return prog;
    }

    public void AddExperience(Guid userId, decimal betAmount, IGameRepository repo, IRealTimeService realTime) {
        var prog = GetProgression(userId, repo);
        
        decimal xpGained = betAmount * XP_PER_CURRENCY_UNIT;
        prog.CurrentXP += xpGained;
        prog.LifetimeXP += xpGained;

        // Check for Level Up
        // Formula: Next Level XP = CurrentLevel * 1000
        decimal requiredXP = prog.CurrentLevel * 1000;

        bool leveledUp = false;
        while (prog.CurrentXP >= requiredXP) {
            prog.CurrentXP -= requiredXP;
            prog.CurrentLevel++;
            prog.SkillPoints++;
            leveledUp = true;
            requiredXP = prog.CurrentLevel * 1000; // Recalculate for next level if multiple levels gained
        }

        repo.UpdateUserProgression(prog);

        if (leveledUp) {
            _ = realTime.NotifyGameUpdate(userId, new {
                Type = "LevelUp",
                NewLevel = prog.CurrentLevel,
                SkillPoints = prog.SkillPoints,
                Message = $"Congratulations! You reached Level {prog.CurrentLevel}!"
            });
        }
    }
}
