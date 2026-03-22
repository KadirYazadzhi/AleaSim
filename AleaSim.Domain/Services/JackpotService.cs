using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AleaSim.Domain.Services;

public class JackpotService : IJackpotService {
    private readonly IRngService _rngService;
    private readonly IRealTimeService _realTimeService;
    private readonly ILockService _lockService;
    private readonly IRedisService _redis;
    private readonly IServiceScopeFactory _scopeFactory;

    public JackpotService(IRngService rng, IRealTimeService realTime, ILockService lockService, IRedisService redis, IServiceScopeFactory scopeFactory) {
        _rngService = rng;
        _realTimeService = realTime;
        _lockService = lockService;
        _redis = redis;
        _scopeFactory = scopeFactory;
    }

    public async Task Contribute(Guid gameId, decimal betAmount, IGameRepository repo) {
        var db = _redis.GetDatabase();
        var jackpots = repo.GetJackpots().ToList();
        
        foreach (var jackpot in jackpots) {
            if (jackpot.Tier == JackpotTier.Mini || jackpot.Tier == JackpotTier.Minor) continue;

            bool shouldContribute = jackpot.IsGlobal || (jackpot.GameId.HasValue && jackpot.GameId.Value == gameId);
            
            if (shouldContribute) {
                // SECURITY: Use distributed lock to prevent race conditions
                using var lockHandle = await _lockService.AcquireLockAsync($"jackpot:{jackpot.Id}", TimeSpan.FromSeconds(3));
                
                decimal increase = betAmount * jackpot.ContributionRate;
                string redisKey = $"jackpot:{jackpot.Id}";

                var redisVal = await db.StringGetAsync(redisKey);
                if (!redisVal.HasValue) {
                    await db.StringSetAsync(redisKey, (double)jackpot.CurrentValue);
                }

                double newValue = await db.StringIncrementAsync(redisKey, (double)increase);
                
                jackpot.CurrentValue = (decimal)newValue;
                jackpot.LastUpdated = DateTime.UtcNow;
                
                // Only sync to DB every few increments to reduce load, or just sync when it crosses a whole number
                if (Math.Floor(newValue) > Math.Floor(newValue - (double)increase)) {
                    repo.UpdateJackpot(jackpot);
                }

                await _realTimeService.NotifyJackpotUpdate(jackpot); 
            }
        }
    }

    public async Task<(bool Triggered, decimal WinAmount)> CheckJackpotTrigger(Guid gameId, int seed, int sequence, IGameRepository repo) {
        double roll = _rngService.GetNextDouble(seed, HashCode.Combine(sequence, "jackpot_trigger"));
        var db = _redis.GetDatabase();
        
        var jackpots = repo.GetJackpots()
            .Where(j => j.Tier != JackpotTier.Mini && j.Tier != JackpotTier.Minor) // Ignore fixed multipliers
            .Where(j => j.IsGlobal || (j.GameId.HasValue && j.GameId.Value == gameId))
            .OrderBy(j => j.Tier)
            .ToList();

        foreach (var j in jackpots) {
            string redisKey = $"jackpot:{j.Id}";
            var redisVal = await db.StringGetAsync(redisKey);
            decimal currentValue = redisVal.HasValue ? (decimal)(double)redisVal : j.CurrentValue;

            decimal pressure = j.MustDropAt.HasValue && j.MustDropAt.Value > 0 ? currentValue / j.MustDropAt.Value : 0.1m;
            
            // Scaled chances based on tier (Mini/Minor removed)
            double baseChance = j.Tier switch {
                JackpotTier.Major => 0.0005,      // 1 in 2000 spins
                JackpotTier.Mega => 0.0001,       // 1 in 10000 spins
                JackpotTier.Tournament => 0.00001, // 1 in 100000 spins
                JackpotTier.Special => 0.001,     // 1 in 1000 spins
                JackpotTier.Grand => 0.00005,     // 1 in 20000 spins
                _ => 0.0001
            };
            
            double threshold = baseChance * (double)pressure; 

            // Forced drop if over Cap
            if (roll < threshold || (j.MustDropAt.HasValue && currentValue >= j.MustDropAt.Value)) {
                using var lockHandle = await _lockService.AcquireLockAsync($"jackpot_claim_{j.Id}", TimeSpan.FromSeconds(5));
                
                redisVal = await db.StringGetAsync(redisKey);
                currentValue = redisVal.HasValue ? (decimal)(double)redisVal : j.CurrentValue;
                decimal resetValue = GetResetValue(j.Tier);

                if (currentValue > resetValue) {
                    decimal win = currentValue;
                    await db.StringSetAsync(redisKey, (double)resetValue);
                    
                    j.CurrentValue = resetValue;
                    j.LastUpdated = DateTime.UtcNow;
                    repo.UpdateJackpot(j);
                    await _realTimeService.NotifyJackpotUpdate(j);
                    
                    if (j.IsGlobal || j.Tier >= JackpotTier.Major) {
                        await _realTimeService.BroadcastMessage("System", $"🏆 BIG WIN! A lucky player just claimed the {j.Name} jackpot for {win:C2}!");
                    }

                    return (true, win);
                }
            }
        }
        
        return (false, 0);
    }

    private decimal GetResetValue(JackpotTier tier) => tier switch {
        JackpotTier.Major => 2500m,
        JackpotTier.Mega => 10000m,
        JackpotTier.Tournament => 25000m,
        JackpotTier.Special => 500m,
        JackpotTier.Grand => 10000m,
        _ => 100m
    };

    public Jackpot GetGlobalJackpot(IGameRepository repo) => repo.GetJackpots().First(j => j.IsGlobal);
    public Jackpot GetLocalJackpot(Guid gameId, IGameRepository repo) => repo.GetOrCreateLocalJackpot(gameId);

    public async Task<decimal> ClaimJackpot(JackpotTier tier, IGameRepository repo) {
        var jackpot = repo.GetJackpots().FirstOrDefault(j => j.Tier == tier && j.IsGlobal);
        if (jackpot == null) return 0m;
        
        using var lockHandle = await _lockService.AcquireLockAsync($"jackpot_claim_{jackpot.Id}", TimeSpan.FromSeconds(5));
        
        var db = _redis.GetDatabase();
        string redisKey = $"jackpot:{jackpot.Id}";
        var redisVal = await db.StringGetAsync(redisKey);
        decimal win = redisVal.HasValue ? (decimal)(double)redisVal : jackpot.CurrentValue;
        
        decimal resetValue = GetResetValue(tier);
        if (win > resetValue) {
            await db.StringSetAsync(redisKey, (double)resetValue);
            jackpot.CurrentValue = resetValue;
            jackpot.LastUpdated = DateTime.UtcNow;
            repo.UpdateJackpot(jackpot);
            await _realTimeService.NotifyJackpotUpdate(jackpot);
            return win;
        }
        return 0m;
    }

    public decimal GetTierValue(JackpotTier tier, IGameRepository repo) {
        var db = _redis.GetDatabase();
        var jackpot = repo.GetJackpots().FirstOrDefault(j => j.Tier == tier && j.IsGlobal);
        if (jackpot == null) return 0m;

        var redisVal = db.StringGet($"jackpot:{jackpot.Id}");
        if (redisVal.HasValue) return (decimal)(double)redisVal;

        return jackpot.CurrentValue;
    }

    public async Task ForceDrop(Guid jackpotId, IGameRepository repo) {
        var db = _redis.GetDatabase();
        var jackpot = repo.GetJackpots().FirstOrDefault(j => j.Id == jackpotId);
        if (jackpot == null) return;

        using var lockHandle = await _lockService.AcquireLockAsync($"jackpot_claim_{jackpot.Id}", TimeSpan.FromSeconds(5));
        
        string redisKey = $"jackpot:{jackpot.Id}";
        var redisVal = await db.StringGetAsync(redisKey);
        decimal win = redisVal.HasValue ? (decimal)(double)redisVal : jackpot.CurrentValue;
        
        decimal resetValue = GetResetValue(jackpot.Tier);
        
        await db.StringSetAsync(redisKey, (double)resetValue);
        jackpot.CurrentValue = resetValue;
        jackpot.LastUpdated = DateTime.UtcNow;
        repo.UpdateJackpot(jackpot);

        await _realTimeService.NotifyJackpotUpdate(jackpot);
        await _realTimeService.BroadcastMessage("System", $"🏆 A massive Jackpot Event has occurred! The {jackpot.Name} has been claimed by a lucky participant!");
    }
}
