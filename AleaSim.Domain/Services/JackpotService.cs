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
    private readonly Guid? _cloverChaseGameId;

    public JackpotService(IRngService rng, IRealTimeService realTime, ILockService lockService, IRedisService redis, IServiceScopeFactory scopeFactory) {
        _rngService = rng;
        _realTimeService = realTime;
        _lockService = lockService;
        _redis = redis;
        _scopeFactory = scopeFactory;
        
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        _cloverChaseGameId = repo.GetGameByType("slot")?.Id;
    }

    public async Task Contribute(Guid gameId, decimal betAmount, IGameRepository repo) {
        var db = _redis.GetDatabase();
        var jackpots = repo.GetJackpots().ToList();
        
        foreach (var j in jackpots) {
            bool shouldContribute = j.IsGlobal; 
            if (!j.IsGlobal && j.GameId == gameId) shouldContribute = true; 
            
            // Mega and Grand are progressive and exclusive to Clover Chase in this logic
            bool isBigJackpot = (j.Tier == JackpotTier.Grand || j.Tier == JackpotTier.Mega);
            if (isBigJackpot) {
                if (gameId != _cloverChaseGameId) continue;
                shouldContribute = true; 
            }

            if (shouldContribute && isBigJackpot) { 
                decimal increase = betAmount * j.ContributionRate;
                string redisKey = $"jackpot:{j.Id}";

                var redisVal = await db.StringGetAsync(redisKey);
                if (!redisVal.HasValue) {
                    await db.StringSetAsync(redisKey, (double)j.CurrentValue);
                }

                double newValue = await db.StringIncrementAsync(redisKey, (double)increase);
                
                j.CurrentValue = (decimal)newValue;
                j.LastUpdated = DateTime.UtcNow;
                
                if (Math.Floor(newValue) > Math.Floor(newValue - (double)increase)) {
                    repo.UpdateJackpot(j);
                }

                await _realTimeService.NotifyJackpotUpdate(j); 
            }
        }
    }

    public async Task<(bool Triggered, decimal WinAmount)> CheckJackpotTrigger(Guid gameId, int seed, int sequence, IGameRepository repo) {
        double roll = _rngService.GetNextDouble(seed, HashCode.Combine(sequence, "jackpot_trigger"));
        var db = _redis.GetDatabase();
        
        var jackpots = repo.GetJackpots()
            .Where(j => j.IsGlobal || j.GameId == gameId)
            .OrderBy(j => j.Tier)
            .ToList();

        foreach (var j in jackpots) {
            if (j.Tier == JackpotTier.Grand || j.Tier == JackpotTier.Mega) {
                if (gameId != _cloverChaseGameId) continue; 
            }

            string redisKey = $"jackpot:{j.Id}";
            var redisVal = await db.StringGetAsync(redisKey);
            decimal currentValue = redisVal.HasValue ? (decimal)(double)redisVal : j.CurrentValue;

            decimal pressure = j.MustDropAt.HasValue ? currentValue / j.MustDropAt.Value : 0.1m;
            double baseChance = j.Tier switch {
                JackpotTier.Mini => 0.002,   // 1 in 500
                JackpotTier.Major => 0.001, // 1 in 1000
                JackpotTier.Mega => 0.0002, // 1 in 5000
                JackpotTier.Grand => 0.00005, // 1 in 20000
                _ => 0.0001
            };
            
            double threshold = baseChance * (double)pressure; 

            if (roll < threshold || (j.MustDropAt.HasValue && currentValue >= j.MustDropAt.Value)) {
                using var lockHandle = await _lockService.AcquireLockAsync($"jackpot_claim_{j.Tier}", TimeSpan.FromSeconds(2));
                
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
                    return (true, win);
                }
            }
        }
        
        return (false, 0);
    }

    private decimal GetResetValue(JackpotTier tier) => tier switch {
        JackpotTier.Mini => 50m,
        JackpotTier.Major => 500m,
        JackpotTier.Mega => 2500m,
        JackpotTier.Grand => 10000m,
        _ => 100m
    };

    public Jackpot GetGlobalJackpot(IGameRepository repo) => repo.GetJackpots().First(j => j.Tier == JackpotTier.Grand && j.IsGlobal);
    public Jackpot GetLocalJackpot(Guid gameId, IGameRepository repo) => repo.GetOrCreateLocalJackpot(gameId);

    public async Task<decimal> ClaimJackpot(JackpotTier tier, IGameRepository repo) {
        using var lockHandle = await _lockService.AcquireLockAsync($"jackpot_claim_{tier}", TimeSpan.FromSeconds(5));
        
        var db = _redis.GetDatabase();
        var jackpot = repo.GetJackpots().FirstOrDefault(j => j.Tier == tier && j.IsGlobal);
        if (jackpot == null) return 0m;

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

        using var lockHandle = await _lockService.AcquireLockAsync($"jackpot_claim_{jackpot.Tier}", TimeSpan.FromSeconds(5));
        
        string redisKey = $"jackpot:{jackpot.Id}";
        decimal resetValue = GetResetValue(jackpot.Tier);
        
        await db.StringSetAsync(redisKey, (double)resetValue);
        jackpot.CurrentValue = resetValue;
        jackpot.LastUpdated = DateTime.UtcNow;
        repo.UpdateJackpot(jackpot);

        await _realTimeService.NotifyJackpotUpdate(jackpot);
        await _realTimeService.BroadcastMessage("System", $"🏆 A massive Jackpot Event has occurred! The {jackpot.Tier} Jackpot has been claimed by a lucky participant!");
    }
}