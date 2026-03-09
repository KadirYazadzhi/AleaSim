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

        _engine = new BaccaratGameEngine(_mockRng.Object, _mockVault.Object, _mockBrain.Object, _mockPromo.Object, _mockJackpot.Object, _mockRealTime.Object, _mockScopeFactory.Object, _mockLock.Object);
    }

    [Fact]
    public async Task ResolveRound_ShouldCalculateWin_WhenPlayerWinsOnPlayerBet() {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = new GameSession { Id = sessionId, UserId = userId, Seed = 12345 };
        var bet = new Bet { GameSessionId = sessionId, Amount = 10m, BetData = "{"Type":"Player"}" };
        
        _mockRepo.Setup(r => r.GetSession(sessionId)).Returns(session);
        _mockRepo.Setup(r => r.GetLastBet(sessionId)).Returns(bet);
        _mockBrain.Setup(b => b.GetNextDirective(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>()))
                  .Returns(new AleaSim.Domain.Models.BrainDirective { DecisionType = "Random" });

        // Draw cards to ensure Player Score > Banker Score
        // 0 = A (1), 1 = 2, 2 = 3...
        // Player: 0 (A), 2 (3) -> 4
        // Banker: 1 (2), 1 (2) -> 4? No, let's pick specific indices.
        // Cards are drawn: P1, B1, P2, B2.
        _mockRng.SetupSequence(r => r.GetNextInt(It.IsAny<int>(), It.IsAny<int>(), 0, 52))
                .Returns(8)  // P1: 9H (9)
                .Returns(0)  // B1: AH (1)
                .Returns(13) // P2: AD (1) -> P Total: 10 % 10 = 0
                .Returns(1)  // B2: 2H (2) -> B Total: 3
                // Wait, Baccarat rules for 3rd card...
                // If P or B has 8 or 9, it's a Natural. 9 vs 3 is not natural? No, 8 or 9.
                // Let's force a natural win for Player.
                .SetReturnsDefault(0);

        _mockRng.Reset();
        _mockRng.SetupSequence(r => r.GetNextInt(It.IsAny<int>(), It.IsAny<int>(), 0, 52))
                .Returns(8)  // P1: 9H (9)
                .Returns(0)  // B1: AH (1)
                .Returns(0)  // P2: AH (1) -> P Total: 10 % 10 = 0. Wait.
                // Resetting logic:
                .Returns(7)  // P1: 8H (8)
                .Returns(0)  // B1: AH (1)
                .Returns(1)  // P2: 2H (2) -> P Total: 10 % 10 = 0. Still 0.
                // Let's use simple cards.
                .Returns(2)  // P1: 3H (3)
                .Returns(0)  // B1: AH (1)
                .Returns(3)  // P2: 4H (4) -> P Total: 7
                .Returns(1); // B2: 2H (2) -> B Total: 3. No natural.
        
        // Player has 7. Banker has 3.
        // P (6 or 7) -> Stands.
        // B (3) -> Draws unless P drew a 3rd card and it was an 8.
        // Here P stands, so B draws if <= 5. B has 3, so draws.
        _mockRng.Setup(r => r.GetNextInt(It.IsAny<int>(), 405, 0, 52)).Returns(0); // B3: AH (1) -> B Total: 4.
        // Final: P (7), B (4). Player Wins.

        // Act
        var round = await _engine.ResolveRound(sessionId);

        // Assert
        Assert.Equal(20m, round.TotalWinAmount); // 10 * 2 = 20
        _mockVault.Verify(v => v.ProcessWinAsync(userId, 20m, _mockRepo.Object), Times.Once);
    }

    [Fact]
    public async Task ResolveRound_ShouldHandleTie_WithEightToOnePayout() {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = new GameSession { Id = sessionId, UserId = userId, Seed = 12345 };
        var bet = new Bet { GameSessionId = sessionId, Amount = 10m, BetData = "{"Type":"Tie"}" };
        
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
        Assert.Equal(90m, round.TotalWinAmount); // 10 * 9 = 90
    }
}
