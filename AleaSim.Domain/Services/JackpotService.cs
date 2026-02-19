using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class JackpotService : IJackpotService {
    private readonly IRngService _rngService;
    private readonly IRealTimeService _realTimeService;
    private readonly ILockService _lockService;
    private readonly IRedisService _redis;
    
    public JackpotService(IRngService rngService, IRealTimeService realTimeService, ILockService lockService, IRedisService redis) {
        _rngService = rngService;
        _realTimeService = realTimeService;
        _lockService = lockService;
        _redis = redis;
    }

    public async Task Contribute(Guid gameId, decimal betAmount, IGameRepository repo) {
        var db = _redis.GetDatabase();
        var jackpots = repo.GetJackpots().ToList();
        
        foreach (var j in jackpots) {
            bool shouldContribute = j.IsGlobal || (j.GameId == gameId);
            
            // Only Spades (MEGA) and Hearts (MAJOR) are progressive
            if (shouldContribute && (j.Tier == JackpotTier.Spades || j.Tier == JackpotTier.Hearts)) {
                decimal increase = betAmount * j.ContributionRate;
                double newValue = await db.StringIncrementAsync($"jackpot:{j.Tier}", (double)increase);
                
                // Sync back to entity for local calculations if needed
                j.CurrentValue = (decimal)newValue;
                j.LastUpdated = DateTime.UtcNow;
                
                // Periodic Sync to DB (e.g. every 10 units of increase or specific interval)
                // For now: Notify real-time only
                _ = _realTimeService.NotifyJackpotUpdate(j); 
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
            // Get LIVE value from Redis if progressive, otherwise from DB
            decimal currentValue = j.CurrentValue;
            if (j.Tier == JackpotTier.Spades || j.Tier == JackpotTier.Hearts) {
                var redisVal = await db.StringGetAsync($"jackpot:{j.Tier}");
                if (redisVal.HasValue) currentValue = (decimal)(double)redisVal;
            }

            decimal pressure = j.MustDropAt.HasValue ? currentValue / j.MustDropAt.Value : 0.1m;
            double baseChance = j.Tier switch {
                JackpotTier.Clubs => 0.001,
                JackpotTier.Diamonds => 0.0005,
                JackpotTier.Hearts => 0.0001,
                JackpotTier.Spades => 0.00001,
                _ => 0.0001
            };
            
            double threshold = baseChance * (double)pressure; 

            if (roll < threshold || (j.MustDropAt.HasValue && currentValue >= j.MustDropAt.Value)) {
                decimal win = currentValue;
                decimal resetValue = GetResetValue(j.Tier);
                
                // Reset in Redis
                await db.StringSetAsync($"jackpot:{j.Tier}", (double)resetValue);
                
                j.CurrentValue = resetValue;
                j.LastUpdated = DateTime.UtcNow;
                repo.UpdateJackpot(j);
                _ = _realTimeService.NotifyJackpotUpdate(j);
                return (true, win);
            }
        }
        
        return (false, 0);
    }

    private decimal GetResetValue(JackpotTier tier) => tier switch {
        JackpotTier.Clubs => 10m,
        JackpotTier.Diamonds => 50m,
        JackpotTier.Hearts => 200m,
        JackpotTier.Spades => 5000m,
        _ => 0m
    };

    public Jackpot GetGlobalJackpot(IGameRepository repo) => repo.GetJackpots().First(j => j.Tier == JackpotTier.Spades);
    public Jackpot GetLocalJackpot(Guid gameId, IGameRepository repo) => repo.GetOrCreateLocalJackpot(gameId);

    public async Task<decimal> ClaimJackpot(JackpotTier tier, IGameRepository repo) {
        var db = _redis.GetDatabase();
        var jackpot = repo.GetJackpots().FirstOrDefault(j => j.Tier == tier);
        if (jackpot == null) return 0m;

        var redisVal = await db.StringGetAsync($"jackpot:{tier}");
        decimal win = redisVal.HasValue ? (decimal)(double)redisVal : jackpot.CurrentValue;
        
        decimal resetValue = GetResetValue(tier);
        await db.StringSetAsync($"jackpot:{tier}", (double)resetValue);

        jackpot.CurrentValue = resetValue;
        jackpot.LastUpdated = DateTime.UtcNow;
            
        repo.UpdateJackpot(jackpot);
        _ = _realTimeService.NotifyJackpotUpdate(jackpot);
            
        return win;
    }

    public decimal GetTierValue(JackpotTier tier, IGameRepository repo) {
        var db = _redis.GetDatabase();
        var redisVal = db.StringGet($"jackpot:{tier}");
        if (redisVal.HasValue) return (decimal)(double)redisVal;

        var jackpot = repo.GetJackpots().FirstOrDefault(j => j.Tier == tier);
        return jackpot?.CurrentValue ?? 0m;
    }
}