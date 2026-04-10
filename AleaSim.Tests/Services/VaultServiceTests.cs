using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AleaSim.Tests.Services;

public class VaultServiceTests {
    private readonly Mock<IRealTimeService> _mockRealTime;
    private readonly Mock<IGameRepository> _mockRepo;
    private readonly Mock<ILockService> _mockLock;
    private readonly Mock<IRedisCacheService> _mockCache; // Added
    private readonly VaultService _vaultService;

    public VaultServiceTests() {
        _mockRealTime = new Mock<IRealTimeService>();
        _mockRealTime.Setup(x => x.NotifyBalanceUpdate(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
                     .Returns(Task.CompletedTask);

        _mockRepo = new Mock<IGameRepository>();
        _mockRepo.Setup(x => x.SaveTransaction(It.IsAny<Transaction>()));
        
        var mockTx = new Mock<ITransaction>();
        _mockRepo.Setup(r => r.BeginTransaction()).Returns(mockTx.Object);

        _mockLock = new Mock<ILockService>();
        _mockCache = new Mock<IRedisCacheService>(); // Init
        _mockCache.Setup(x => x.RemoveAsync(It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

        // Setup lock to return a disposable
        _mockLock.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                 .ReturnsAsync(new Mock<IDisposable>().Object);

        var mockLogger = new Mock<ILogger<VaultService>>();
        _vaultService = new VaultService(_mockRealTime.Object, _mockLock.Object, _mockCache.Object, mockLogger.Object);
    }

    [Fact]
    public async Task ProcessBet_ShouldDeductFromRealBalance_WhenNoBonus() {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Balance = 100m, BonusBalance = 0m };
        _mockRepo.Setup(r => r.GetUser(userId)).Returns(user);

        // Act
        bool result = await _vaultService.ProcessBetAsync(userId, 10m, _mockRepo.Object);

        // Assert
        Assert.True(result);
        Assert.Equal(90m, user.Balance);
        _mockRepo.Verify(r => r.UpdateUser(user), Times.Once);
    }

    [Fact]
    public async Task ProcessBet_ShouldDeductFromBonus_WhenBonusAvailable() {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { 
            Id = userId, 
            Balance = 100m, 
            BonusBalance = 50m, 
            WageringRequirement = 500m, 
            WageringProgress = 0m 
        };
        _mockRepo.Setup(r => r.GetUser(userId)).Returns(user);

        // Act
        bool result = await _vaultService.ProcessBetAsync(userId, 10m, _mockRepo.Object);

        // Assert
        Assert.True(result);
        Assert.Equal(40m, user.BonusBalance); // Deducted from Bonus
        Assert.Equal(100m, user.Balance);     // Real untouched
        Assert.Equal(10m, user.WageringProgress); // Progress increased
    }

    [Fact]
    public async Task ProcessBet_ShouldSplitDeduction_WhenBonusInsufficient() {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { 
            Id = userId, 
            Balance = 100m, 
            BonusBalance = 5m, // Only 5 bonus
            WageringRequirement = 500m,
            WageringProgress = 0m
        };
        _mockRepo.Setup(r => r.GetUser(userId)).Returns(user);

        // Act
        // Bet 10 (5 from Bonus, 5 from Real)
        bool result = await _vaultService.ProcessBetAsync(userId, 10m, _mockRepo.Object);

        // Assert
        Assert.True(result);
        Assert.Equal(0m, user.BonusBalance);
        Assert.Equal(95m, user.Balance);
        Assert.Equal(5m, user.WageringProgress); 
    }

    [Fact]
    public async Task ProcessBet_ShouldFail_WhenTotalInsufficient() {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Balance = 5m, BonusBalance = 0m };
        _mockRepo.Setup(r => r.GetUser(userId)).Returns(user);

        // Act
        bool result = await _vaultService.ProcessBetAsync(userId, 10m, _mockRepo.Object);

        // Assert
        Assert.False(result);
        Assert.Equal(5m, user.Balance); // Untouched
        _mockRepo.Verify(r => r.UpdateUser(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWin_ShouldCreditToReal_WhenNoBonusActive() {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Balance = 100m, BonusBalance = 0m };
        _mockRepo.Setup(r => r.GetUser(userId)).Returns(user);

        // Act
        await _vaultService.ProcessWinAsync(userId, 50m, _mockRepo.Object);

        // Assert
        Assert.Equal(150m, user.Balance);
        Assert.Equal(0m, user.BonusBalance);
    }

    [Fact]
    public async Task ProcessWin_ShouldCreditToBonus_WhenBonusActive() {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { 
            Id = userId, 
            Balance = 100m, 
            BonusBalance = 10m, 
            WageringRequirement = 100m, 
            WageringProgress = 10m 
        };
        _mockRepo.Setup(r => r.GetUser(userId)).Returns(user);

        // Act
        await _vaultService.ProcessWinAsync(userId, 50m, _mockRepo.Object);

        // Assert
        Assert.Equal(100m, user.Balance); // Real untouched
        Assert.Equal(60m, user.BonusBalance); // Added to bonus
    }

    [Fact]
    public async Task ProcessBet_ShouldAccumulateCashback_WhenBetIsPlaced() {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Balance = 100m };
        var profile = new PlayerProfile { UserId = userId, CashbackLevel = 0, PendingCashback = 0m };
        
        _mockRepo.Setup(r => r.GetUser(userId)).Returns(user);
        _mockRepo.Setup(r => r.GetPlayerProfile(userId)).Returns(profile);

        // Act
        // Bet 10. Base cashback is 10%. 10 * 0.10 = 1.00
        await _vaultService.ProcessBetAsync(userId, 10m, _mockRepo.Object);

        // Assert
        Assert.Equal(1.00m, profile.PendingCashback);
    }

    [Fact]
    public async Task ClaimCashback_ShouldTransferFundsToBalance() {
        // ... (unchanged)
    }

    [Fact]
    public async Task ProcessWin_ShouldBeIdempotent_WhenReferenceIdIsProvided() {
        // Arrange
        var userId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var user = new User { Id = userId, Balance = 100m };
        _mockRepo.Setup(r => r.GetUser(userId)).Returns(user);
        
        // Mock that transaction already exists
        var existingTx = new Transaction { Id = roundId };
        _mockRepo.Setup(r => r.GetTransaction(roundId)).Returns(existingTx);

        // Act
        await _vaultService.ProcessWinAsync(userId, 50m, _mockRepo.Object, roundId);

        // Assert
        Assert.Equal(100m, user.Balance); // Balance should NOT change
        _mockRepo.Verify(r => r.SaveTransaction(It.IsAny<Transaction>()), Times.Never);
    }
}
