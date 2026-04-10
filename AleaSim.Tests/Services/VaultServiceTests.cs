using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AleaSim.Tests.Services;

public class VaultServiceTests {
    private readonly Mock<IRealTimeService> _mockRealTime;
    private readonly Mock<IGameRepository> _mockRepo;
    private readonly Mock<ILockService> _mockLock;
    private readonly Mock<IRedisCacheService> _mockCache;
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
        _mockCache = new Mock<IRedisCacheService>();
        _mockCache.Setup(x => x.RemoveAsync(It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

        _mockLock.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                 .ReturnsAsync(new Mock<IDisposable>().Object);

        var mockLogger = new Mock<ILogger<VaultService>>();
        _vaultService = new VaultService(_mockRealTime.Object, _mockLock.Object, _mockCache.Object, mockLogger.Object);
    }

    [Fact]
    public async Task ProcessBet_ShouldDeductFromRealBalance_WhenNoBonus() {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Balance = 100m, BonusBalance = 0m };
        _mockRepo.Setup(r => r.GetUser(userId)).Returns(user);

        bool result = await _vaultService.ProcessBetAsync(userId, 10m, _mockRepo.Object);

        Assert.True(result);
        Assert.Equal(90m, user.Balance);
        _mockRepo.Verify(r => r.UpdateUser(user), Times.Once);
    }

    [Fact]
    public async Task ProcessBet_ShouldDeductFromBonus_WhenBonusAvailable() {
        var userId = Guid.NewGuid();
        var user = new User { 
            Id = userId, Balance = 100m, BonusBalance = 50m, 
            WageringRequirement = 500m, WageringProgress = 0m 
        };
        _mockRepo.Setup(r => r.GetUser(userId)).Returns(user);

        bool result = await _vaultService.ProcessBetAsync(userId, 10m, _mockRepo.Object);

        Assert.True(result);
        Assert.Equal(40m, user.BonusBalance);
        Assert.Equal(10m, user.WageringProgress);
    }

    [Fact]
    public async Task ProcessWin_ShouldCreditToReal_WhenNoBonusActive() {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Balance = 100m, BonusBalance = 0m };
        _mockRepo.Setup(r => r.GetUser(userId)).Returns(user);

        await _vaultService.ProcessWinAsync(userId, 50m, _mockRepo.Object);

        Assert.Equal(150m, user.Balance);
    }

    [Fact]
    public async Task ClaimCashback_ShouldTransferFundsToBalance() {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Balance = 100m };
        var profile = new PlayerProfile { UserId = userId, PendingCashback = 5.50m };
        
        _mockRepo.Setup(r => r.GetUser(userId)).Returns(user);
        _mockRepo.Setup(r => r.GetPlayerProfile(userId)).Returns(profile);

        // Act
        var amount = await _vaultService.ClaimCashbackAsync(userId, _mockRepo.Object);

        // Assert
        Assert.Equal(5.50m, amount);
        Assert.Equal(105.50m, user.Balance);
        Assert.Equal(0m, profile.PendingCashback);
        _mockRepo.Verify(r => r.UpdateUser(user), Times.Once);
        _mockRepo.Verify(r => r.UpdatePlayerProfile(profile), Times.Once);
    }

    [Fact]
    public async Task ProcessWin_ShouldBeIdempotent_WhenReferenceIdIsProvided() {
        var userId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var user = new User { Id = userId, Balance = 100m };
        _mockRepo.Setup(r => r.GetUser(userId)).Returns(user);
        
        var existingTx = new Transaction { Id = roundId };
        _mockRepo.Setup(r => r.GetTransaction(roundId)).Returns(existingTx);

        await _vaultService.ProcessWinAsync(userId, 50m, _mockRepo.Object, roundId);

        Assert.Equal(100m, user.Balance); 
        _mockRepo.Verify(r => r.SaveTransaction(It.IsAny<Transaction>()), Times.Never);
    }
}
