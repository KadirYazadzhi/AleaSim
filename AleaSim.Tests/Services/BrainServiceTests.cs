using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models;
using AleaSim.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace AleaSim.Tests.Services;

public class BrainServiceTests {
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IVaultService> _mockVault;
    private readonly Mock<IGameRepository> _mockRepo;
    private readonly BrainService _brainService;

    public BrainServiceTests() {
        _mockRepo = new Mock<IGameRepository>();
        _mockVault = new Mock<IVaultService>();
        
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceProvider.Setup(x => x.GetService(typeof(IGameRepository))).Returns(_mockRepo.Object);

        _mockScope = new Mock<IServiceScope>();
        _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);

        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);

        _brainService = new BrainService(_mockScopeFactory.Object, _mockVault.Object);
    }

    [Fact]
    public void DecideOutcome_ShouldTriggerRetentionHook_WhenLossStreakHigh() {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var profile = new PlayerProfile { 
            UserId = userId, 
            LossStreak = 10, // High streak > 8
            TotalWagered = 1000,
            CurrentSessionRtp = 0.2m,
            SymbolAffinityJson = "{}"
        };
        _mockRepo.Setup(r => r.GetPlayerProfile(userId)).Returns(profile);
        _mockVault.Setup(v => v.CanAffordWin(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), _mockRepo.Object, It.IsAny<bool>())).Returns(true);

        // Act
        var directive = _brainService.DecideOutcome(userId, gameId, 1.0m, _mockRepo.Object);

        // Assert
        Assert.Equal("RetentionHook", directive.DecisionType);
        Assert.True(directive.TargetWinAmount > 0);
    }

    [Fact]
    public void DecideOutcome_ShouldTriggerWhaleProtocol_WhenBetHigh() {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var profile = new PlayerProfile { UserId = userId, SymbolAffinityJson = "{}" };
        _mockRepo.Setup(r => r.GetPlayerProfile(userId)).Returns(profile);
        
        // Setup Vault to afford the big win (simulate 20% chance hit logic inside Brain)
        // Note: Since Brain uses Random, we can't guarantee the 20% branch hit without seeding or trying loop.
        // However, the "Else" branch of Whale Protocol is "WhaleLoss" (Target 0).
        // Standard "Random" should NOT be returned.
        
        // Act
        var directive = _brainService.DecideOutcome(userId, gameId, 100m, _mockRepo.Object);

        // Assert
        Assert.Contains(directive.DecisionType, new[] { "WhaleBonus", "WhaleLoss" });
        Assert.NotEqual("Random", directive.DecisionType);
    }

    [Fact]
    public void DecideOutcome_ShouldTriggerCoolDown_WhenRtpTooHigh() {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var profile = new PlayerProfile { 
            UserId = userId, 
            ActualRtp = 2.5, // 250% RTP!
            TotalWagered = 500,
            SymbolAffinityJson = "{}"
        };
        _mockRepo.Setup(r => r.GetPlayerProfile(userId)).Returns(profile);

        // Act
        var directive = _brainService.DecideOutcome(userId, gameId, 1.0m, _mockRepo.Object);

        // Assert
        Assert.Equal("CoolDown", directive.DecisionType);
        Assert.True(directive.IsNearMiss); // Must force teaser
        Assert.Equal(0m, directive.TargetWinAmount);
    }
}