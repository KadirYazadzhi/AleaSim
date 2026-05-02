using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text.Json;
using Xunit;

namespace AleaSim.Tests.Services;

public class BlackjackGameEngineTests {
    private readonly Mock<IRngService> _mockRng;
    private readonly Mock<IVaultService> _mockVault;
    private readonly Mock<IBrainService> _mockBrain;
    private readonly Mock<IPromotionService> _mockPromo;
    private readonly Mock<IJackpotService> _mockJackpot;
    private readonly Mock<IRealTimeService> _mockRealTime;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IGameRepository> _mockRepo;
    private readonly Mock<ILockService> _mockLock;
    private readonly BlackjackGameEngine _engine;

    public BlackjackGameEngineTests() {
        _mockRng = new Mock<IRngService>();
        _mockVault = new Mock<IVaultService>();
        _mockBrain = new Mock<IBrainService>();
        _mockPromo = new Mock<IPromotionService>();
        _mockJackpot = new Mock<IJackpotService>();
        _mockRealTime = new Mock<IRealTimeService>();

        _mockRealTime.Setup(x => x.NotifyGameUpdate(It.IsAny<Guid>(), It.IsAny<object>()))
                     .Returns(Task.CompletedTask);
        
        _mockVault.Setup(x => x.ProcessWinAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>(), It.IsAny<Guid?>()))
                  .Returns(Task.CompletedTask);
        _mockVault.Setup(x => x.ProcessBetAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>(), It.IsAny<Guid?>()))
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

        _engine = new BlackjackGameEngine(_mockRng.Object, _mockVault.Object, _mockBrain.Object, _mockPromo.Object, _mockJackpot.Object, _mockRealTime.Object, _mockScopeFactory.Object, _mockLock.Object);
    }

    [Fact]
    public async Task ResolveRound_ShouldHandleBlackjack_WithThreeToTwoPayout() {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var shoe = new List<string>(Enumerable.Repeat("10S", 60));
        shoe[0] = "AH"; // P1
        shoe[1] = "2H"; // D1
        shoe[2] = "JH"; // P2
        shoe[3] = "3H"; // D2
        
        var state = new BlackjackGameEngine.BlackjackState { Shoe = shoe };
        var session = new GameSession { 
            Id = sessionId, 
            UserId = userId, 
            ServerSeed = "test", 
            ClientSeed = "client",
            GameState = JsonSerializer.Serialize(state)
        };
        var bet = new Bet { GameSessionId = sessionId, Amount = 10m };
        
        _mockRepo.Setup(r => r.GetSession(sessionId)).Returns(session);
        _mockRepo.Setup(r => r.GetLastBet(sessionId)).Returns(bet);
        _mockBrain.Setup(b => b.GetNextDirectiveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>()))
                  .ReturnsAsync(new AleaSim.Domain.Models.BrainDirective { DecisionType = "Random" });

        // Act
        var round = await _engine.ResolveRound(sessionId);

        // Assert
        Assert.Equal(25m, round.TotalWinAmount); // 10 * 2.5 = 25
        _mockVault.Verify(v => v.ProcessWinAsync(userId, 25m, _mockRepo.Object, It.IsAny<Guid?>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAction_ShouldHandleHit_Correctly() {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = new GameSession { Id = sessionId, UserId = userId, ServerSeed = "test", ClientSeed = "client" };
        var state = new BlackjackGameEngine.BlackjackState {
            PlayerHands = new List<BlackjackGameEngine.BlackjackHand> {
                new BlackjackGameEngine.BlackjackHand { Cards = new List<string> { "2H", "3H" }, Bet = 10m }
            },
            DealerHand = new List<string> { "5H", "6H" },
            ActiveHandIndex = 0,
            IsRoundOver = false
        };
        var round = new GameRound { 
            Id = Guid.NewGuid(), 
            GameSessionId = sessionId, 
            RandomResult = JsonSerializer.Serialize(state),
            RoundNumber = 1
        };

        _mockRepo.Setup(r => r.GetSession(sessionId)).Returns(session);
        _mockRepo.Setup(r => r.GetLastRound(sessionId)).Returns(round);
        
        // Act
        await _engine.ProcessAction(userId, sessionId, "Hit", "{}");

        // Assert
        // Verify state was updated and saved
        _mockRepo.Verify(r => r.SaveRound(It.Is<GameRound>(rd => rd.Id == round.Id)), Times.Once);
    }
}
