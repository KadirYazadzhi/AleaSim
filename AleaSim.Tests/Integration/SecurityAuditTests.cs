using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace AleaSim.Tests.Integration;

public class SecurityAuditTests {
    private readonly IServiceProvider _serviceProvider;
    private readonly string _connString = "Server=localhost;Port=3306;Database=aleasim_test;User=root;Password=secretpassword;";

    public SecurityAuditTests() {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AleaSimDbContext>(options => 
            options.UseMySql(_connString, ServerVersion.AutoDetect(_connString)));
        services.AddScoped<IGameRepository, EfGameRepository>();
        
        // Mock dependencies
        services.AddSingleton(new Mock<IRngService>().Object);
        services.AddSingleton(new Mock<IBrainService>().Object);
        services.AddSingleton(new Mock<IPromotionService>().Object);
        services.AddSingleton(new Mock<IJackpotService>().Object);
        services.AddSingleton(new Mock<IRealTimeService>().Object);
        services.AddSingleton(new Mock<IRedisCacheService>().Object);
        services.AddSingleton(new Mock<ILockService>().Object);
        services.AddSingleton(new Mock<IAuditService>().Object);
        services.AddSingleton(new Mock<IBackgroundTaskQueue>().Object);
        services.AddScoped<IVaultService, VaultService>();
        services.AddScoped<IQuestService, QuestService>();
        services.AddScoped<ILevelService, LevelService>();

        _serviceProvider = services.BuildServiceProvider();

        // Setup Test DB
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Vault_ShouldRejectNegativeDeductions() {
        using var scope = _serviceProvider.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<IVaultService>();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();

        var user = new User { Id = Guid.NewGuid(), Username = "SecurityUser", Balance = 1000 };
        repo.CreateUser(user);

        // Act
        var result = await vault.ProcessBetAsync(user.Id, -500m, repo, Guid.NewGuid());
        
        // Assert
        Assert.False(result); // Vault handles invalid decimals gracefully by returning false
        var updatedUser = repo.GetUser(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(1000m, updatedUser.Balance); 
    }

    [Fact]
    public async Task Vault_ShouldRejectInsufficientFunds() {
        using var scope = _serviceProvider.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<IVaultService>();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();

        var user = new User { Id = Guid.NewGuid(), Username = "PoorUser", Balance = 10 };
        repo.CreateUser(user);

        // Act & Assert
        var result = await vault.ProcessBetAsync(user.Id, 100m, repo, Guid.NewGuid());
        Assert.False(result);
    }
}
