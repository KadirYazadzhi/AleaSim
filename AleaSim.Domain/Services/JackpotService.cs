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
        var cloverChaseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        
        foreach (var j in jackpots) {
            bool shouldContribute = j.IsGlobal || (j.GameId == gameId);
            
            // EXCLUSIVE RULE: Mega (Spades) and Major (Hearts) are only for Clover Chase
            if (j.Tier == JackpotTier.Spades || j.Tier == JackpotTier.Hearts) {
                if (gameId != cloverChaseId) continue; // Skip contribution from other games
            }

            // Only Spades (MEGA) and Hearts (MAJOR) are progressive in this implementation
            if (shouldContribute && (j.Tier == JackpotTier.Spades || j.Tier == JackpotTier.Hearts)) {
                decimal increase = betAmount * j.ContributionRate;
                string redisKey = $"jackpot:{j.Id}"; // Use ID for absolute uniqueness

                // WARM-UP LOGIC: Ensure Redis has the current DB value if empty
                var redisVal = await db.StringGetAsync(redisKey);
                if (!redisVal.HasValue) {
                    await db.StringSetAsync(redisKey, (double)j.CurrentValue);
                }

                double newValue = await db.StringIncrementAsync(redisKey, (double)increase);
                
                // Sync to Entity
                j.CurrentValue = (decimal)newValue;
                j.LastUpdated = DateTime.UtcNow;
                
                // PERIODIC DB SYNC: Save to DB every ~1.00 unit increase to avoid DB stress
                if (Math.Floor(newValue) > Math.Floor(newValue - (double)increase)) {
                    repo.UpdateJackpot(j);
                }

                _ = _realTimeService.NotifyJackpotUpdate(j); 
            }
        }
    }

    public async Task<(bool Triggered, decimal WinAmount)> CheckJackpotTrigger(Guid gameId, int seed, int sequence, IGameRepository repo) {
        double roll = _rngService.GetNextDouble(seed, HashCode.Combine(sequence, "jackpot_trigger"));
        var db = _redis.GetDatabase();
        var cloverChaseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        
        var jackpots = repo.GetJackpots()
            .Where(j => j.IsGlobal || j.GameId == gameId)
            .OrderBy(j => j.Tier)
            .ToList();

        foreach (var j in jackpots) {
            // EXCLUSIVE RULE: Mega (Spades) and Major (Hearts) are only for Clover Chase
            if (j.Tier == JackpotTier.Spades || j.Tier == JackpotTier.Hearts) {
                if (gameId != cloverChaseId) continue; 
            }

            // ALWAYS get live value from Redis
            string redisKey = $"jackpot:{j.Id}";
            var redisVal = await db.StringGetAsync(redisKey);
            decimal currentValue = redisVal.HasValue ? (decimal)(double)redisVal : j.CurrentValue;

            decimal pressure = j.MustDropAt.HasValue ? currentValue / j.MustDropAt.Value : 0.1m;
            double baseChance = j.Tier switch {
                JackpotTier.Clubs => 0.002,   // 1 in 500
                JackpotTier.Diamonds => 0.001, // 1 in 1000
                JackpotTier.Hearts => 0.0002, // 1 in 5000
                JackpotTier.Spades => 0.00005, // 1 in 20000
                _ => 0.0001
            };
            
            double threshold = baseChance * (double)pressure; 

            if (roll < threshold || (j.MustDropAt.HasValue && currentValue >= j.MustDropAt.Value)) {
                using var lockHandle = await _lockService.AcquireLockAsync($"jackpot_claim_{j.Tier}", TimeSpan.FromSeconds(2));
                
                // Re-verify after lock
                redisVal = await db.StringGetAsync(redisKey);
                currentValue = redisVal.HasValue ? (decimal)(double)redisVal : j.CurrentValue;
                decimal resetValue = GetResetValue(j.Tier);

                if (currentValue > resetValue) {
                    decimal win = currentValue;
                    
                    // Reset in Redis
                    await db.StringSetAsync(redisKey, (double)resetValue);
                    
                    j.CurrentValue = resetValue;
                    j.LastUpdated = DateTime.UtcNow;
                    repo.UpdateJackpot(j);
                    _ = _realTimeService.NotifyJackpotUpdate(j);
                    return (true, win);
                }
            }
        }
        
        return (false, 0);
    }

    private decimal GetResetValue(JackpotTier tier) => tier switch {
        JackpotTier.Clubs => 50m,
        JackpotTier.Diamonds => 500m,
        JackpotTier.Hearts => 2500m,
        JackpotTier.Spades => 10000m,
        _ => 100m
    };

    public Jackpot GetGlobalJackpot(IGameRepository repo) => repo.GetJackpots().First(j => j.Tier == JackpotTier.Spades && j.IsGlobal);
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
        // Only reset if it hasn't been reset yet (concurrency check)
        if (win > resetValue) {
            await db.StringSetAsync(redisKey, (double)resetValue);

            jackpot.CurrentValue = resetValue;
            jackpot.LastUpdated = DateTime.UtcNow;
                
            repo.UpdateJackpot(jackpot);
            _ = _realTimeService.NotifyJackpotUpdate(jackpot);
                
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
}