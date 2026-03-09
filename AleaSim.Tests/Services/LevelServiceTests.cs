using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Shared.Models;
using Moq;
using Xunit;

namespace AleaSim.Tests.Services;

public class LevelServiceTests {
    private readonly Mock<IAchievementService> _mockAchievement;
    private readonly Mock<IVaultService> _mockVault;
    private readonly Mock<ILockService> _mockLock;
    private readonly Mock<IGameRepository> _mockRepo;
    private readonly Mock<IRealTimeService> _mockRealTime;
    private readonly LevelService _levelService;

    public LevelServiceTests() {
        _mockAchievement = new Mock<IAchievementService>();
        _mockVault = new Mock<IVaultService>();
        _mockLock = new Mock<ILockService>();
        _mockRepo = new Mock<IGameRepository>();
        _mockRealTime = new Mock<IRealTimeService>();

        _mockLock.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                 .ReturnsAsync(new Mock<IDisposable>().Object);

        _levelService = new LevelService(_mockAchievement.Object, _mockVault.Object, _mockLock.Object);
    }

    [Fact]
    public async Task AddExperience_ShouldIncreaseXP_BasedOnBetAmount() {
        // Arrange
        var userId = Guid.NewGuid();
        var prog = new UserProgression { UserId = userId, CurrentLevel = 1, CurrentXP = 0 };
        var profile = new PlayerProfile { UserId = userId, XpBoostLevel = 0 };
        
        _mockRepo.Setup(r => r.GetUserProgression(userId)).Returns(prog);
        _mockRepo.Setup(r => r.GetPlayerProfile(userId)).Returns(profile);

        // Act
        // 10 units * 10 XP/unit = 100 XP
        await _levelService.AddExperience(userId, 10m, _mockRepo.Object, _mockRealTime.Object);

        // Assert
        Assert.Equal(100m, prog.CurrentXP);
        _mockRepo.Verify(r => r.UpdateUserProgression(prog), Times.Once);
        _mockRealTime.Verify(r => r.NotifyProgressionUpdate(userId, It.IsAny<UserProgressionDto>()), Times.Once);
    }

    [Fact]
    public async Task AddExperience_ShouldApplyXpBoost_FromProfile() {
        // Arrange
        var userId = Guid.NewGuid();
        var prog = new UserProgression { UserId = userId, CurrentLevel = 1, CurrentXP = 0 };
        var profile = new PlayerProfile { UserId = userId, XpBoostLevel = 2 }; // +20% Boost
        
        _mockRepo.Setup(r => r.GetUserProgression(userId)).Returns(prog);
        _mockRepo.Setup(r => r.GetPlayerProfile(userId)).Returns(profile);

        // Act
        // 10 units * 10 XP/unit * 1.20 = 120 XP
        await _levelService.AddExperience(userId, 10m, _mockRepo.Object, _mockRealTime.Object);

        // Assert
        Assert.Equal(120m, prog.CurrentXP);
    }

    [Fact]
    public async Task AddExperience_ShouldLevelUp_WhenThresholdReached() {
        // Arrange
        var userId = Guid.NewGuid();
        // Level 1 threshold is 1 * 1000 = 1000 XP
        var prog = new UserProgression { UserId = userId, CurrentLevel = 1, CurrentXP = 950 };
        var profile = new PlayerProfile { UserId = userId, XpBoostLevel = 0 };
        
        _mockRepo.Setup(r => r.GetUserProgression(userId)).Returns(prog);
        _mockRepo.Setup(r => r.GetPlayerProfile(userId)).Returns(profile);

        // Act
        // 10 units * 10 XP = 100 XP. 950 + 100 = 1050.
        // LevelUp! 1050 - 1000 = 50 XP remaining in Lv 2.
        await _levelService.AddExperience(userId, 10m, _mockRepo.Object, _mockRealTime.Object);

        // Assert
        Assert.Equal(2, prog.CurrentLevel);
        Assert.Equal(50m, prog.CurrentXP);
        Assert.Equal(1, prog.SkillPoints);
        
        _mockAchievement.Verify(a => a.CheckAchievements(userId, "LevelReached", 2, _mockRepo.Object), Times.Once);
        _mockRealTime.Verify(r => r.NotifyGameUpdate(userId, It.Is<object>(o => o.ToString()!.Contains("LevelUp"))), Times.Once);
    }

    [Fact]
    public async Task AddExperience_ShouldCreditBonus_EveryFiveLevels() {
        // Arrange
        var userId = Guid.NewGuid();
        var prog = new UserProgression { UserId = userId, CurrentLevel = 4, CurrentXP = 3950 }; // Threshold for Lv 4 is 4000
        var profile = new PlayerProfile { UserId = userId };
        
        _mockRepo.Setup(r => r.GetUserProgression(userId)).Returns(prog);
        _mockRepo.Setup(r => r.GetPlayerProfile(userId)).Returns(profile);

        // Act
        // Bet enough to reach Level 5
        await _levelService.AddExperience(userId, 10m, _mockRepo.Object, _mockRealTime.Object);

        // Assert
        Assert.Equal(5, prog.CurrentLevel);
        // Milestone prize for Lv 5 = 5 * 10 = 50
        _mockVault.Verify(v => v.CreditBonusAsync(userId, 50m, 50m, _mockRepo.Object), Times.Once);
    }
}
