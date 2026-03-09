using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;
using System.Text.Json;

namespace AleaSim.Tests.Services;

public class DiceGameEngineTests {
    private readonly Mock<IRngService> _mockRng;
    private readonly Mock<IVaultService> _mockVault;
    private readonly Mock<IBrainService> _mockBrain;
    private readonly Mock<IPromotionService> _mockPromo;
    private readonly Mock<IJackpotService> _mockJackpot;
    private readonly Mock<IRealTimeService> _mockRealTime;
    private readonly Mock<ILockService> _mockLock;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IGameRepository> _mockRepo;
    private readonly IMemoryCache _cache;
    private readonly DiceGameEngine _engine;

    public DiceGameEngineTests() {
        _mockRng = new Mock<IRngService>();
        _mockVault = new Mock<IVaultService>();
        _mockBrain = new Mock<IBrainService>();
        _mockPromo = new Mock<IPromotionService>();
        _mockJackpot = new Mock<IJackpotService>();
        _mockRealTime = new Mock<IRealTimeService>();
        _mockLock = new Mock<ILockService>();
        _mockRepo = new Mock<IGameRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceProvider.Setup(x => x.GetService(typeof(IGameRepository))).Returns(_mockRepo.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IQuestService))).Returns(new Mock<IQuestService>().Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(ILevelService))).Returns(new Mock<ILevelService>().Object);

        _mockScope = new Mock<IServiceScope>();
        _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);

        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);

        _mockLock.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                 .ReturnsAsync(new Mock<IDisposable>().Object);

        var mockTx = new Mock<ITransaction>();
        _mockRepo.Setup(r => r.BeginTransaction()).Returns(mockTx.Object);

        _engine = new DiceGameEngine(_mockRng.Object, _mockVault.Object, _mockBrain.Object, _mockPromo.Object, _mockJackpot.Object, _mockRealTime.Object, _mockScopeFactory.Object, _mockLock.Object, _cache);
    }

    [Fact]
    public async Task ResolveRound_ShouldCalculateWin_WhenSliderTargetHit() {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = new GameSession { Id = sessionId, UserId = userId, Seed = 12345 };
        // Target > 50.50. Win if result is > 50.50
        var betData = new { Mode = "Slider", Type = "Slider", Target = 50.50m, IsOver = true };
        var bet = new Bet { GameSessionId = sessionId, Amount = 10m, BetData = JsonSerializer.Serialize(betData) };
        
        _mockRepo.Setup(r => r.GetSession(sessionId)).Returns(session);
        _mockRepo.Setup(r => r.GetLastBet(sessionId)).Returns(bet);
        _mockBrain.Setup(b => b.GetNextDirective(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>()))
                  .Returns(new AleaSim.Domain.Models.BrainDirective { DecisionType = "Random" });

        // Force roll 75.00
        _mockRng.Setup(r => r.GetNextDouble(It.IsAny<int>(), It.IsAny<int>())).Returns(0.75);

        // Act
        var round = await _engine.ResolveRound(sessionId);

        // Assert
        // Chance = 100 - 50.50 = 49.50. Payout = 99 / 49.5 = 2x.
        Assert.Equal(20m, round.TotalWinAmount);
        _mockVault.Verify(v => v.ProcessWinAsync(userId, 20m, _mockRepo.Object), Times.Once);
    }

    [Fact]
    public async Task ResolveRound_ShouldHandleMultiMode_Correctly() {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = new GameSession { Id = sessionId, UserId = userId, Seed = 12345 };
        // Selected numbers: 6. 1/6 chance. Payout = 99/16.66 = ~5.94x.
        var betData = new { Mode = "Multi", Numbers = new[] { 6 } };
        var bet = new Bet { GameSessionId = sessionId, Amount = 10m, BetData = JsonSerializer.Serialize(betData) };
        
        _mockRepo.Setup(r => r.GetSession(sessionId)).Returns(session);
        _mockRepo.Setup(r => r.GetLastBet(sessionId)).Returns(bet);
        _mockBrain.Setup(b => b.GetNextDirective(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>()))
                  .Returns(new AleaSim.Domain.Models.BrainDirective { DecisionType = "Random" });

        // Force roll 6
        _mockRng.Setup(r => r.GetNextInt(It.IsAny<int>(), It.IsAny<int>(), 1, 7)).Returns(6);

        // Act
        var round = await _engine.ResolveRound(sessionId);

        // Assert
        Assert.Equal(59.4m, Math.Round(round.TotalWinAmount, 2));
    }
}
