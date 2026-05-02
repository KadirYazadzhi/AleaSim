using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace AleaSim.Tests.Services;

public class QuestServiceTests {
    private readonly Mock<IGameRepository> _repo;
    private readonly Mock<IRealTimeService> _realTime;
    private readonly Mock<IVaultService> _vault;
    private readonly QuestService _service;

    public QuestServiceTests() {
        _repo = new Mock<IGameRepository>();
        _realTime = new Mock<IRealTimeService>();
        _vault = new Mock<IVaultService>();
        _service = new QuestService();
    }

    [Fact]
    public async Task ProcessWin_ShouldProgressWinAmountQuests() {
        var userId = Guid.NewGuid();
        var questId = Guid.NewGuid();
        var progId = Guid.NewGuid();

        var activeQuests = new List<Quest> {
            new Quest { Id = questId, GoalType = "WinAmount", TargetValue = 100, RewardAmount = 10, IsActive = true }
        };

        var userProgressions = new List<UserQuestProgress> {
            new UserQuestProgress {
                Id = progId,
                UserId = userId,
                QuestId = questId,
                CurrentValue = 0,
                IsCompleted = false,
                Quest = activeQuests[0]
            }
        };

        _repo.Setup(r => r.GetAllQuests()).Returns(activeQuests);
        _repo.Setup(r => r.GetUserQuestProgressions(userId)).Returns(userProgressions);

        await _service.UpdateProgressAsync(userId, "WinAmount", 50m, _repo.Object, _realTime.Object, _vault.Object);

        Assert.Equal(50m, userProgressions[0].CurrentValue);
        Assert.False(userProgressions[0].IsCompleted);
        _repo.Verify(r => r.UpdateUserQuestProgress(It.IsAny<UserQuestProgress>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessAction_ShouldCompleteQuestAndRewardUser() {
        var userId = Guid.NewGuid();
        var questId = Guid.NewGuid();
        var progId = Guid.NewGuid();

        var activeQuests = new List<Quest> {
            new Quest { Id = questId, GoalType = "SpinCount", TargetValue = 100, RewardAmount = 10, Title = "Spin 100", IsActive = true }
        };

        var userProgressions = new List<UserQuestProgress> {
            new UserQuestProgress {
                Id = progId,
                UserId = userId,
                QuestId = questId,
                CurrentValue = 90,
                IsCompleted = false,
                Quest = activeQuests[0]
            }
        };

        _repo.Setup(r => r.GetAllQuests()).Returns(activeQuests);
        _repo.Setup(r => r.GetUserQuestProgressions(userId)).Returns(userProgressions);
        
        var user = new User { Id = userId, BonusBalance = 0 };
        _repo.Setup(r => r.GetUser(userId)).Returns(user);

        // Action value 10 to reach 100
        await _service.UpdateProgressAsync(userId, "SpinCount", 10m, _repo.Object, _realTime.Object, _vault.Object);

        Assert.True(userProgressions[0].IsCompleted);
        // Reward is credited via vault.CreditBonusAsync
        _vault.Verify(v => v.CreditBonusAsync(userId, 10m, It.IsAny<decimal>(), _repo.Object), Times.Once);
        _repo.Verify(r => r.UpdateUserQuestProgress(It.IsAny<UserQuestProgress>()), Times.Once);
    }
}
