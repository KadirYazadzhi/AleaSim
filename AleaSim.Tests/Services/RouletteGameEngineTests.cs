using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using System.Text.Json;

namespace AleaSim.Tests.Services;

public class RouletteGameEngineTests {
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
    private readonly RouletteGameEngine _engine;

    public RouletteGameEngineTests() {
        _mockRng = new Mock<IRngService>();
        _mockVault = new Mock<IVaultService>();
        _mockBrain = new Mock<IBrainService>();
        _mockPromo = new Mock<IPromotionService>();
        _mockJackpot = new Mock<IJackpotService>();
        _mockRealTime = new Mock<IRealTimeService>();
        _mockLock = new Mock<ILockService>();
        _mockRepo = new Mock<IGameRepository>();

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

        _engine = new RouletteGameEngine(_mockRng.Object, _mockVault.Object, _mockBrain.Object, _mockPromo.Object, _mockJackpot.Object, _mockRealTime.Object, _mockScopeFactory.Object, _mockLock.Object);
    }

    [Fact]
    public async Task ResolveRound_ShouldCalculateWin_WhenRedBetHitsRedNumber() {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = new GameSession { Id = sessionId, UserId = userId, Seed = 12345 };
        var betData = new { 
            Bets = new[] { new { Type = "color", Value = "red", Amount = 10m } },
            Mode = "Classic"
        };
        var bet = new Bet { GameSessionId = sessionId, Amount = 10m, BetData = JsonSerializer.Serialize(betData) };
        
        _mockRepo.Setup(r => r.GetSession(sessionId)).Returns(session);
        _mockRepo.Setup(r => r.GetLastBet(sessionId)).Returns(bet);
        _mockBrain.Setup(b => b.GetNextDirective(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>()))
                  .Returns(new AleaSim.Domain.Models.BrainDirective { DecisionType = "Random" });

        // Force number 1 (Red)
        _mockRng.Setup(r => r.GetNextInt(It.IsAny<int>(), It.IsAny<int>(), 0, 37)).Returns(1);

        // Act
        var round = await _engine.ResolveRound(sessionId);

        // Assert
        Assert.Equal(20m, round.TotalWinAmount); // 10 * 2 = 20
        _mockVault.Verify(v => v.ProcessWinAsync(userId, 20m, _mockRepo.Object), Times.Once);
    }

    [Fact]
    public async Task ResolveRound_ShouldHandleExtremeMode_WithMultipliers() {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = new GameSession { Id = sessionId, UserId = userId, Seed = 12345 };
        var betData = new { 
            Bets = new[] { new { Type = "number", Value = "17", Amount = 10m } },
            Mode = "Extreme"
        };
        var bet = new Bet { GameSessionId = sessionId, Amount = 10m, BetData = JsonSerializer.Serialize(betData) };
        
        _mockRepo.Setup(r => r.GetSession(sessionId)).Returns(session);
        _mockRepo.Setup(r => r.GetLastBet(sessionId)).Returns(bet);
        _mockBrain.Setup(b => b.GetNextDirective(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>()))
                  .Returns(new AleaSim.Domain.Models.BrainDirective { DecisionType = "Random" });

        // Force number 17
        _mockRng.Setup(r => r.GetNextInt(It.IsAny<int>(), It.IsAny<int>(), 0, 37)).Returns(17);
        
        // Force a multiplier for extreme mode
        // Logic in RouletteGameEngine uses RNG to pick lucky numbers
        _mockRng.Setup(r => r.GetNextInt(It.IsAny<int>(), It.IsAny<int>(), 1, 6)).Returns(3); // 3 lucky numbers
        _mockRng.Setup(r => r.GetNextInt(It.IsAny<int>(), It.IsAny<int>(), 0, 37)).Returns(17); // 17 is lucky
        _mockRng.Setup(r => r.GetNextInt(It.IsAny<int>(), It.IsAny<int>(), 1, 11)).Returns(5); // 50x multiplier (5 * 10)

        // Act
        var round = await _engine.ResolveRound(sessionId);

        // Assert
        // Standard Straight win is 30x in Extreme. Multiplier is 50x.
        // Win = 10 * 50 = 500.
        Assert.Equal(500m, round.TotalWinAmount);
    }
}
