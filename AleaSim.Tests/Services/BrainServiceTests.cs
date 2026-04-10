using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models;
using AleaSim.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Microsoft.Extensions.Caching.Memory;

namespace AleaSim.Tests.Services;

public class BrainServiceTests {
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IGameRepository> _mockRepo;
    private readonly Mock<IRngService> _mockRng;
    private readonly Mock<IRedisCacheService> _mockRedis;
    private readonly IMemoryCache _cache;
    private readonly BrainService _brainService;

    public BrainServiceTests() {
        _mockRepo = new Mock<IGameRepository>();
        _mockRepo.Setup(r => r.GetGlobalSetting("GlobalShadowMode")).Returns("false");
        _mockRepo.Setup(r => r.GetGlobalSetting("GlobalTargetRtp")).Returns("95");
        _mockRepo.Setup(r => r.GetGlobalSetting("VolatilityMode")).Returns("Medium");

        _mockRng = new Mock<IRngService>();
        _mockRedis = new Mock<IRedisCacheService>();
        _mockRedis.Setup(x => x.GetAsync<List<BrainDirective>>(It.IsAny<string>()))
                  .ReturnsAsync(new List<BrainDirective>());
        _mockRedis.Setup(x => x.GetAsync<PlayerProfile>(It.IsAny<string>()))
                  .Returns(Task.FromResult<PlayerProfile?>(null));
        _mockRedis.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()))
                  .Returns(Task.CompletedTask);
        
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceProvider.Setup(x => x.GetService(typeof(IGameRepository))).Returns(_mockRepo.Object);

        _mockScope = new Mock<IServiceScope>();
        _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);

        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);

        _cache = new MemoryCache(new MemoryCacheOptions()); 

        _brainService = new BrainService(_mockScopeFactory.Object, _cache, _mockRedis.Object, _mockRng.Object);
    }

    [Fact]
    public void DecideOutcome_ShouldTriggerRetentionHook_WhenLossStreakHigh() {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var profile = new PlayerProfile { 
            UserId = userId, 
            LossStreak = 10,
            TotalWagered = 1000,
            CurrentSessionRtp = 0.2m,
            SymbolAffinityJson = "{}"
        };
        _mockRepo.Setup(r => r.GetPlayerProfile(userId)).Returns(profile);
        _mockRepo.Setup(r => r.GetGame(gameId)).Returns(new Game { Id = gameId, PoolBalance = 1000000m });
        
        _mockRng.Setup(r => r.GetNextInt(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>())).Returns(15);

        // Act
        var directive = _brainService.DecideOutcome(userId, gameId, 1.0m, _mockRepo.Object);

        // Assert
        Assert.Equal("RetentionHook", directive.DecisionType);
        Assert.True(directive.TargetWinAmount > 0);
    }

    [Fact]
    public void DecideOutcome_ShouldTriggerCoolDown_WhenRtpTooHigh() {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var profile = new PlayerProfile { 
            UserId = userId, 
            ActualRtp = 3.0m, // decimal literal
            TotalWagered = 500,
            SymbolAffinityJson = "{}"
        };
        _mockRepo.Setup(r => r.GetPlayerProfile(userId)).Returns(profile);
        _mockRepo.Setup(r => r.GetGame(gameId)).Returns(new Game { Id = gameId, PoolBalance = 1000000m });
        
        _mockRng.Setup(r => r.GetNextInt(It.IsAny<int>(), It.IsAny<int>(), 0, 100)).Returns(10);

        // Act
        var directive = _brainService.DecideOutcome(userId, gameId, 1.0m, _mockRepo.Object);

        // Assert
        Assert.Equal("Random", directive.DecisionType);
        Assert.Equal("RTP Correction (High)", directive.Reason);
        Assert.Equal(0m, directive.TargetWinAmount);
    }
}
