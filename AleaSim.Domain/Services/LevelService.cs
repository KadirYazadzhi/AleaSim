using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class LevelService : ILevelService {
    private const decimal XP_PER_CURRENCY_UNIT = 10m; 
    private readonly IAchievementService _achievementService;
    private readonly IVaultService _vaultService;

    public LevelService(IAchievementService achievementService, IVaultService vaultService) {
        _achievementService = achievementService;
        _vaultService = vaultService;
    }

    public UserProgression GetProgression(Guid userId, IGameRepository repo) {
        return repo.GetUserProgression(userId) ?? CreateDefault(userId, repo);
    }

    private UserProgression CreateDefault(Guid userId, IGameRepository repo) {
        var prog = new UserProgression { Id = Guid.NewGuid(), UserId = userId };
        repo.CreateUserProgression(prog);
        return prog;
    }

    public async Task AddExperience(Guid userId, decimal betAmount, IGameRepository repo, IRealTimeService realTime) {
        var prog = GetProgression(userId, repo);
        var profile = repo.GetPlayerProfile(userId);
        
        decimal multiplier = 1.0m + ((profile?.XpBoostLevel ?? 0) * 0.10m);
        decimal xpGained = betAmount * XP_PER_CURRENCY_UNIT * multiplier;
        
        prog.CurrentXP += xpGained;
        prog.LifetimeXP += xpGained;

        decimal requiredXP = prog.CurrentLevel * 1000;

        bool leveledUp = false;
        while (prog.CurrentXP >= requiredXP) {
            prog.CurrentXP -= requiredXP;
            prog.CurrentLevel++;
            prog.SkillPoints++;
            leveledUp = true;
            requiredXP = prog.CurrentLevel * 1000; 
        }

        repo.UpdateUserProgression(prog);

        if (leveledUp) {
            _ = _achievementService.CheckAchievements(userId, "LevelReached", prog.CurrentLevel, repo);

            if (prog.CurrentLevel % 5 == 0) {
                decimal milestonePrize = prog.CurrentLevel * 10m; 
                await _vaultService.CreditBonusAsync(userId, milestonePrize, milestonePrize, repo);
            }

            _ = realTime.NotifyGameUpdate(userId, new {
                Type = "LevelUp",
                NewLevel = prog.CurrentLevel,
                SkillPoints = prog.SkillPoints,
                Message = $"Congratulations! You reached Level {prog.CurrentLevel}!"
            });
        }
    }

    public async Task<bool> UpgradeSkill(Guid userId, string skillName, IGameRepository repo) {
        var prog = repo.GetUserProgression(userId);
        var profile = repo.GetPlayerProfile(userId);

        if (prog == null || profile == null || prog.SkillPoints <= 0) return false;

        bool success = false;
        switch (skillName.ToLower()) {
            case "clover":
                profile.LuckyCloverLevel++;
                success = true;
                break;
            case "cashback":
                profile.CashbackLevel++;
                success = true;
                break;
            case "xp":
                profile.XpBoostLevel++;
                success = true;
                break;
        }

        if (success) {
            prog.SkillPoints--;
            repo.UpdateUserProgression(prog);
            repo.UpdatePlayerProfile(profile);
        }

        return await Task.FromResult(success);
    }
}