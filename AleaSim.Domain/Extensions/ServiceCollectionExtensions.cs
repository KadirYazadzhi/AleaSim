using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;

namespace AleaSim.Domain.Extensions;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddAleaSimCore(this IServiceCollection services) {
        
        services.AddMemoryCache(options => {
            // SECURITY: Set absolute limits to prevent OutOfMemory via cache-stuffing attacks (Issue 3)
            options.SizeLimit = 1024 * 100; // e.g., 100,000 "units" (each cache entry should specify Size = 1)
        }); 
        services.AddSingleton<ILockService, RedisLockService>(); // Switched to Redis distributed locking
        services.AddSingleton<IBackgroundTaskQueue>(new BackgroundTaskQueue(1000));

        // Core Engines & Logic
        services.AddSingleton<IRngService, DeterministicRngService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IVaultService, VaultService>();
        services.AddSingleton<IBrainService, BrainService>();
        services.AddSingleton<IRedisService, RedisService>();
        services.AddSingleton<IRedisCacheService, RedisCacheService>();
        services.AddSingleton<IJackpotService, JackpotService>();
        services.AddSingleton<IAuditBuffer, AuditBuffer>();
        services.AddSingleton<ILeaderboardService, LeaderboardService>();
        services.AddSingleton<IPromotionService, PromotionService>();
        services.AddSingleton<IAuditService, AuditService>();

        // Scoped Services (Per Request/Command)
        services.AddScoped<IQuestService, QuestService>();
        services.AddScoped<AleaSim.Domain.Interfaces.IAchievementService, AleaSim.Domain.Services.AchievementService>();
        services.AddScoped<IVoucherService, VoucherService>();
        services.AddScoped<ITournamentService, TournamentService>();
        services.AddScoped<ILevelService, LevelService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ISimulationService, SimulationService>();
        services.AddScoped<IGameDirector, GameDirector>();

        // Game Engines
        services.AddSingleton<SlotGameEngine>();
        services.AddSingleton<RouletteGameEngine>();
        services.AddSingleton<BlackjackGameEngine>();
        services.AddSingleton<BaccaratGameEngine>();
        services.AddSingleton<FruitBlastGameEngine>();
        services.AddSingleton<DiceGameEngine>(sp => new DiceGameEngine(
            sp.GetRequiredService<IRngService>(),
            sp.GetRequiredService<IVaultService>(),
            sp.GetRequiredService<IBrainService>(),
            sp.GetRequiredService<IPromotionService>(),
            sp.GetRequiredService<IJackpotService>(),
            sp.GetRequiredService<IRealTimeService>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILockService>(),
            sp.GetRequiredService<IRedisCacheService>()
        ));

        // Game Factory Strategy
        services.AddSingleton<Func<string, IGame>>(serviceProvider => key => {
            return key.ToLower() switch {
                "slot" => serviceProvider.GetRequiredService<SlotGameEngine>(),
                "roulette" => serviceProvider.GetRequiredService<RouletteGameEngine>(),
                "blackjack" => serviceProvider.GetRequiredService<BlackjackGameEngine>(),
                "baccarat" => serviceProvider.GetRequiredService<BaccaratGameEngine>(),
                "dice" => serviceProvider.GetRequiredService<DiceGameEngine>(),
                "fruitblast" => serviceProvider.GetRequiredService<FruitBlastGameEngine>(),
                _ => throw new KeyNotFoundException($"Game engine '{key}' not found")
            };
        });

        return services;
    }
}