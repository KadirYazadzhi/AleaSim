using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace AleaSim.Tests.Integration;

public class ConcurrencyTests {
    private readonly IServiceProvider _serviceProvider;
    private readonly string _connString = "Server=localhost;Port=3306;Database=aleasim_concurrency;User=root;Password=secretpassword;";

    public ConcurrencyTests() {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AleaSimDbContext>(options => 
            options.UseMySql(_connString, ServerVersion.AutoDetect(_connString)));
        services.AddScoped<IGameRepository, EfGameRepository>();
        services.AddScoped<IVaultService, VaultService>();
        
        // Use a REAL Lock Service (InMemory) to test concurrency properly
        services.AddSingleton<ILockService, InMemoryLockService>();
        
        // Mock other services
        services.AddSingleton(new Mock<IRealTimeService>().Object);
        services.AddSingleton(new Mock<ILevelService>().Object);
        services.AddSingleton(new Mock<IQuestService>().Object);
        services.AddSingleton(new Mock<IRedisCacheService>().Object);
        services.AddSingleton(new Mock<IAuditService>().Object);
        services.AddSingleton(new Mock<IBackgroundTaskQueue>().Object);

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Vault_ProcessBet_ShouldBeThreadSafe() {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var vault = scope.ServiceProvider.GetRequiredService<IVaultService>();

        var userId = Guid.NewGuid();
        var user = new User { 
            Id = userId, 
            Username = "ConcurrencyUser", 
            Balance = 100m 
        };
        repo.CreateUser(user);

        int threads = 10;
        decimal deduction = 20m;
        // Total = 200m. Balance = 100m. Exactly 5 should succeed.

        var tasks = new List<Task<bool>>();

        // Act
        for (int i = 0; i < threads; i++) {
            tasks.Add(Task.Run(async () => {
                using var innerScope = _serviceProvider.CreateScope();
                var innerVault = innerScope.ServiceProvider.GetRequiredService<IVaultService>();
                var innerRepo = innerScope.ServiceProvider.GetRequiredService<IGameRepository>();
                var innerLock = innerScope.ServiceProvider.GetRequiredService<ILockService>();
                
                // Simulate BaseGameEngine behavior: Lock THEN Transaction
                using (await innerLock.AcquireLockAsync($"wallet_{userId}", TimeSpan.FromSeconds(30))) {
                    using (var tx = innerRepo.BeginTransaction()) {
                        try {
                            // IMPORTANT: We need to make sure we are not getting a cached user
                            // In EF, since this is a new scope/context, it should hit DB.
                            var success = await innerVault.ProcessBetAsync(userId, deduction, innerRepo, Guid.NewGuid());
                            if (success) {
                                innerRepo.SaveChanges();
                                tx.Commit();
                                return true;
                            }
                            tx.Rollback();
                            return false;
                        } catch {
                            tx.Rollback();
                            return false;
                        }
                    }
                }
            }));
        }

        var results = await Task.WhenAll(tasks);
        int successCount = results.Count(r => r);

        // Assert
        using var finalScope = _serviceProvider.CreateScope();
        var finalRepo = finalScope.ServiceProvider.GetRequiredService<IGameRepository>();
        var finalUser = finalRepo.GetUser(userId);
        
        Assert.NotNull(finalUser);
        Assert.Equal(5, successCount); 
        Assert.Equal(0m, finalUser.Balance);
    }
}
