using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using Moq;
using Xunit;

namespace AleaSim.Tests.Services;

public class VaultServiceTests {
    private readonly Mock<IRealTimeService> _mockRealTime;
    private readonly Mock<IGameRepository> _mockRepo;
    private readonly Mock<ILockService> _mockLock;
    private readonly VaultService _vaultService;

    public VaultServiceTests() {
        _mockRealTime = new Mock<IRealTimeService>();
        _mockRepo = new Mock<IGameRepository>();
        _mockLock = new Mock<ILockService>();

        // Setup lock to return a disposable
        _mockLock.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                 .ReturnsAsync(new Mock<IDisposable>().Object);

        _vaultService = new VaultService(_mockRealTime.Object, _mockLock.Object);
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
    public async Task CheckWageringCompletion_ShouldConvertBonusToReal() {
        // Arrange
        var userId = Guid.NewGuid();
        // Setup a user who is 10 units away from completion
        var user = new User { 
            Id = userId, 
            Balance = 100m, 
            BonusBalance = 50m, 
            WageringRequirement = 100m, 
            WageringProgress = 90m 
        };
        _mockRepo.Setup(r => r.GetUser(userId)).Returns(user);

        // Act
        // Process a bet of 10. This logic is inside ProcessBet -> CheckWageringCompletion
        await _vaultService.ProcessBetAsync(userId, 10m, _mockRepo.Object);

        // Assert
        // 1. Bet 10 deducted from Bonus (50 -> 40)
        // 2. Progress (90 -> 100). 100 >= 100 -> Complete!
        // 3. Conversion: Real = 100 + 40 = 140. Bonus = 0.
        Assert.Equal(140m, user.Balance);
        Assert.Equal(0m, user.BonusBalance);
        Assert.Equal(0m, user.WageringRequirement);
    }
}
