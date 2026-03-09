using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using System.Text.Json;

namespace AleaSim.Tests.Services;

public class BaccaratGameEngineTests {
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
    private readonly BaccaratGameEngine _engine;

    public BaccaratGameEngineTests() {
        _mockRng = new Mock<IRngService>();
        _mockVault = new Mock<IVaultService>();
        _mockBrain = new Mock<IBrainService>();
        _mockPromo = new Mock<IPromotionService>();
        _mockJackpot = new Mock<IJackpotService>();
        _mockJackpot.Setup(x => x.Contribute(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>()))
                    .Returns(Task.CompletedTask);

        _mockRealTime = new Mock<IRealTimeService>();
        _mockRealTime.Setup(x => x.NotifyGameUpdate(It.IsAny<Guid>(), It.IsAny<object>()))
                     .Returns(Task.CompletedTask);
        _mockRealTime.Setup(x => x.NotifyBigWin(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
                     .Returns(Task.CompletedTask);

        _mockVault.Setup(x => x.ProcessWinAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>()))
                  .Returns(Task.CompletedTask);
        _mockVault.Setup(x => x.ProcessBetAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>()))
                  .ReturnsAsync(true);
        _mockVault.Setup(x => x.CanAffordWinAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>(), It.IsAny<bool>()))
                  .ReturnsAsync(true);

        _mockLock = new Mock<ILockService>();
        _mockRepo = new Mock<IGameRepository>();

        var mockQuest = new Mock<IQuestService>();
        mockQuest.Setup(x => x.GenerateDailyQuests(It.IsAny<Guid>(), It.IsAny<IGameRepository>()))
                 .Returns(Task.CompletedTask);
        mockQuest.Setup(x => x.UpdateProgressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>(), It.IsAny<IRealTimeService>(), It.IsAny<IVaultService>()))
                 .Returns(Task.CompletedTask);

        var mockLevel = new Mock<ILevelService>();
        mockLevel.Setup(x => x.AddExperience(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>(), It.IsAny<IRealTimeService>()))
                 .Returns(Task.CompletedTask);

        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceProvider.Setup(x => x.GetService(typeof(IGameRepository))).Returns(_mockRepo.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IQuestService))).Returns(mockQuest.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(ILevelService))).Returns(mockLevel.Object);

        _mockScope = new Mock<IServiceScope>();
        _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);

        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);

        _mockLock.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                 .ReturnsAsync(new Mock<IDisposable>().Object);

        var mockTx = new Mock<ITransaction>();
        _mockRepo.Setup(r => r.BeginTransaction()).Returns(mockTx.Object);

        _engine = new BaccaratGameEngine(_mockRng.Object, _mockVault.Object, _mockBrain.Object, _mockPromo.Object, _mockJackpot.Object, _mockRealTime.Object, _mockScopeFactory.Object, _mockLock.Object);
    }

    [Fact]
    public async Task ResolveRound_ShouldCalculateWin_WhenPlayerWinsOnPlayerBet() {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = new GameSession { Id = sessionId, UserId = userId, Seed = 12345 };
        var bet = new Bet { 
            GameSessionId = sessionId, 
            Amount = 10m, 
            BetData = "{\"Type\":\"Player\"}" 
        };
        
        _mockRepo.Setup(r => r.GetSession(sessionId)).Returns(session);
        _mockRepo.Setup(r => r.GetLastBet(sessionId)).Returns(bet);
        _mockBrain.Setup(b => b.GetNextDirective(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>()))
                  .Returns(new AleaSim.Domain.Models.BrainDirective { DecisionType = "Random" });

        _mockRng.SetupSequence(r => r.GetNextInt(It.IsAny<int>(), It.IsAny<int>(), 0, 52))
                .Returns(2)  // P1: 3H (3)
                .Returns(0)  // B1: AH (1)
                .Returns(3)  // P2: 4H (4) -> P Total: 7
                .Returns(1); // B2: 2H (2) -> B Total: 3. No natural.
        
        _mockRng.Setup(r => r.GetNextInt(It.IsAny<int>(), 105, 0, 52)).Returns(0); // B3: AH (1) -> B Total: 4.

        // Act
        var round = await _engine.ResolveRound(sessionId);

        // Assert
        Assert.Equal(20m, round.TotalWinAmount);
        _mockVault.Verify(v => v.ProcessWinAsync(userId, 20m, _mockRepo.Object), Times.Once);
    }

    [Fact]
    public async Task ResolveRound_ShouldHandleTie_WithEightToOnePayout() {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = new GameSession { Id = sessionId, UserId = userId, Seed = 12345 };
        var bet = new Bet { 
            GameSessionId = sessionId, 
            Amount = 10m, 
            BetData = "{\"Type\":\"Tie\"}" 
        };
        
        _mockRepo.Setup(r => r.GetSession(sessionId)).Returns(session);
        _mockRepo.Setup(r => r.GetLastBet(sessionId)).Returns(bet);
        _mockBrain.Setup(b => b.GetNextDirective(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>()))
                  .Returns(new AleaSim.Domain.Models.BrainDirective { DecisionType = "Random" });

        // Force a Natural Tie (9-9)
        _mockRng.SetupSequence(r => r.GetNextInt(It.IsAny<int>(), It.IsAny<int>(), 0, 52))
                .Returns(8) // P1: 9 (Value 9)
                .Returns(8) // B1: 9 (Value 9)
                .Returns(9) // P2: 10 (Value 0)
                .Returns(9);// B2: 10 (Value 0)

        // Act
        var round = await _engine.ResolveRound(sessionId);

        // Assert
        Assert.Equal(90m, round.TotalWinAmount);
    }
}
