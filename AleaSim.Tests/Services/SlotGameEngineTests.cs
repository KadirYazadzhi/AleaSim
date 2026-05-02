using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace AleaSim.Tests.Services;

public class SlotGameEngineTests {
    private readonly Mock<IRngService> _rng;
    private readonly Mock<IVaultService> _vault;
    private readonly Mock<IBrainService> _brain;
    private readonly Mock<IPromotionService> _promo;
    private readonly Mock<IJackpotService> _jackpot;
    private readonly Mock<IRealTimeService> _realTime;
    private readonly Mock<IServiceScopeFactory> _scopeFactory;
    private readonly Mock<IServiceScope> _scope;
    private readonly Mock<IServiceProvider> _provider;
    private readonly Mock<IRedisCacheService> _cache;
    private readonly Mock<ILockService> _lock;
    private readonly Mock<IGameRepository> _repo;
    private readonly Mock<IQuestService> _quest;
    private readonly Mock<ILevelService> _level;
    
    private readonly SlotGameEngine _engine;

    public SlotGameEngineTests() {
        _rng = new Mock<IRngService>();
        _vault = new Mock<IVaultService>();
        _brain = new Mock<IBrainService>();
        _promo = new Mock<IPromotionService>();
        _jackpot = new Mock<IJackpotService>();
        _realTime = new Mock<IRealTimeService>();
        _scopeFactory = new Mock<IServiceScopeFactory>();
        _scope = new Mock<IServiceScope>();
        _provider = new Mock<IServiceProvider>();
        _cache = new Mock<IRedisCacheService>();
        _lock = new Mock<ILockService>();
        _repo = new Mock<IGameRepository>();
        _quest = new Mock<IQuestService>();
        _level = new Mock<ILevelService>();

        _provider.Setup(p => p.GetService(typeof(IGameRepository))).Returns(_repo.Object);
        _provider.Setup(p => p.GetService(typeof(IQuestService))).Returns(_quest.Object);
        _provider.Setup(p => p.GetService(typeof(ILevelService))).Returns(_level.Object);
        _scope.Setup(s => s.ServiceProvider).Returns(_provider.Object);
        _scopeFactory.Setup(f => f.CreateScope()).Returns(_scope.Object);

        _brain.Setup(b => b.GetNextDirectiveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<IGameRepository>()))
              .ReturnsAsync(new BrainDirective { DecisionType = "Random" });

        _repo.Setup(r => r.GetGameByType("Slot")).Returns(new Game { Id = Guid.NewGuid(), Name = "Clover Chase" });
        _repo.Setup(r => r.BeginTransaction()).Returns(new Mock<ITransaction>().Object);

        _engine = new SlotGameEngine(_rng.Object, _vault.Object, _brain.Object, _promo.Object, _jackpot.Object, _realTime.Object, _scopeFactory.Object, _cache.Object, _lock.Object);
    }

    [Fact]
    public async Task GetCurrentState_ShouldReturnValidState() {
        var sessionId = Guid.NewGuid();
        _repo.Setup(r => r.GetSession(sessionId)).Returns(new GameSession { GameState = "{}" });
        var state = await _engine.GetCurrentState(sessionId);
        Assert.NotNull(state);
    }

    [Fact]
    public async Task PlaceBet_ShouldExecuteAndDeductFunds() {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        
        _repo.Setup(r => r.GetSessionAsync(sessionId)).ReturnsAsync(new GameSession { Id = sessionId, UserId = userId, GameId = gameId });
        _vault.Setup(v => v.ProcessBetAsync(userId, 10m, _repo.Object, It.IsAny<Guid>())).ReturnsAsync(true);

        await _engine.PlaceBet(userId, sessionId, 10m, "{}");

        _vault.Verify(v => v.ProcessBetAsync(userId, 10m, _repo.Object, It.IsAny<Guid>()), Times.Once);
    }
}
