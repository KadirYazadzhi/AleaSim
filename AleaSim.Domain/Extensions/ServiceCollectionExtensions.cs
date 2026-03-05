using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AleaSim.Domain.Extensions;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddAleaSimCore(this IServiceCollection services) {
        
        services.AddMemoryCache(); // Added
        services.AddSingleton<ILockService, InMemoryLockService>(); // Added Lock Service

        // Core Engines & Logic
        services.AddSingleton<IRngService, DeterministicRngService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IVaultService, VaultService>();
        services.AddSingleton<IBrainService, BrainService>();
        services.AddSingleton<IRedisService, RedisService>();
        services.AddSingleton<IRedisCacheService, RedisCacheService>();
        services.AddSingleton<IJackpotService, JackpotService>();
        services.AddSingleton<ILeaderboardService, LeaderboardService>();
        services.AddSingleton<IPromotionService, PromotionService>();
        services.AddSingleton<IAuditService, AuditService>();

        // Scoped Services (Per Request/Command)
        services.AddScoped<IQuestService, QuestService>();
        services.AddScoped<IAchievementService, AchievementService>();
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
        services.AddSingleton<DiceGameEngine>();

        // Game Factory Strategy
        services.AddSingleton<Func<string, IGame>>(serviceProvider => key => {
            return key.ToLower() switch {
                "slot" => serviceProvider.GetRequiredService<SlotGameEngine>(),
                "roulette" => serviceProvider.GetRequiredService<RouletteGameEngine>(),
                "blackjack" => serviceProvider.GetRequiredService<BlackjackGameEngine>(),
                "dice" => serviceProvider.GetRequiredService<DiceGameEngine>(),
                _ => throw new KeyNotFoundException($"Game engine '{key}' not found")
            };
        });

        return services;
    }
}