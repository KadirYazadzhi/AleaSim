using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace AleaSim.Domain.Services;

public class BrainService : IBrainService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVaultService _vaultService;
    private readonly IMemoryCache _cache; 
    private readonly IRedisCacheService _redisCache;
    private readonly IRngService _rngService;

    public BrainService(IServiceScopeFactory scopeFactory, IVaultService vaultService, IMemoryCache cache, IRedisCacheService redisCache, IRngService rngService) {
        _scopeFactory = scopeFactory;
        _vaultService = vaultService;
        _cache = cache;
        _redisCache = redisCache;
        _rngService = rngService;
    }

    public BrainDirective GetNextDirective(Guid userId, Guid gameId, decimal betAmount, IGameRepository repo) {
        string forceKey = $"brain_force_{userId}";
        if (_cache.TryGetValue(forceKey, out BrainDirective? forced) && forced != null) {
            _cache.Remove(forceKey);
            return forced;
        }

        string queueKey = $"brain_queue_{userId}";
        var queue = _redisCache.GetAsync<List<BrainDirective>>(queueKey).GetAwaiter().GetResult();

        if (queue == null || queue.Count == 0) {
            queue = new List<BrainDirective>();
            for (int i = 0; i < 5; i++) {
                queue.Add(DecideOutcome(userId, gameId, betAmount, repo));
            }
        }

        var directive = queue[0];
        queue.RemoveAt(0);
        
        _redisCache.SetAsync(queueKey, queue, TimeSpan.FromMinutes(20)).GetAwaiter().GetResult();
        
        return directive;
    }

    public void SetForcedDirective(Guid userId, BrainDirective directive) {
        _cache.Set($"brain_force_{userId}", directive, TimeSpan.FromMinutes(10));
    }

    public BrainDirective DecideOutcome(Guid userId, Guid gameId, decimal betAmount, IGameRepository repo, bool isShadowMode = false) {
        // 1. Check Forced Directives (Debug/Admin)
        string forceKey = $"brain_force_{userId}";
        if (_cache.TryGetValue(forceKey, out BrainDirective? forced) && forced != null) {
            _cache.Remove(forceKey);
            return forced;
        }

        // 2. Load Profile (TRY REDIS FIRST)
        string cacheKey = $"user:profile:{userId}";
        PlayerProfile? profile = _redisCache.GetAsync<PlayerProfile>(cacheKey).GetAwaiter().GetResult();
        
        if (profile == null) {
            profile = repo.GetPlayerProfile(userId); 
            if (profile != null) {
                _redisCache.SetAsync(cacheKey, profile, TimeSpan.FromMinutes(30)).GetAwaiter().GetResult();
            }
        }

        if (profile == null) return new BrainDirective { DecisionType = "Random" };

        // 2.5 Global Shadow Mode Check
        var globalShadow = repo.GetGlobalSetting("GlobalShadowMode");
        if (!string.IsNullOrEmpty(globalShadow) && globalShadow.ToLower() == "true") {
            return new BrainDirective { DecisionType = "Random", Reason = "Global Shadow Mode Active" };
        }

        if (profile.User != null && profile.User.Username.StartsWith("Sim_")) {
            return new BrainDirective { DecisionType = "Random", VolatilityModifier = 1.0, Reason = "Simulation Standard" };
        }

        decimal globalRtp = 95.0m;
        if (decimal.TryParse(repo.GetGlobalSetting("GlobalTargetRtp"), out var rtpVal)) globalRtp = rtpVal;

        string volMode = repo.GetGlobalSetting("VolatilityMode") ?? "Standard"; // Low, Standard, High

        // 4. Flow State & Volatility
        bool isSimulation = profile.User?.Username?.StartsWith("Sim_") == true;
        bool isInFlow = !isSimulation && profile.AvgSpinInterval < 2.5;
        bool isBored = !isSimulation && profile.AvgSpinInterval > 7.0;
        double volatility = isInFlow ? 2.0 : (isBored ? 0.5 : 1.0);

        // Adjust based on Global Volatility Setting
        if (volMode == "Low") {
            volatility = Math.Max(0.5, volatility * 0.7);
        } else if (volMode == "High") {
            volatility *= 1.5;
        }

        int seedMain = HashCode.Combine(userId, gameId, betAmount, DateTime.UtcNow.Ticks);

        // 5. RTP Correction Logic
        // If user is winning too much (> Target + 10%), force cool down
        if (profile.TotalWagered > 100 && profile.ActualRtp > (double)((globalRtp + 10) / 100)) {
             var randCool = _rngService.GetNextInt(seedMain, 1, 0, 100);
             int coolProb = volMode == "Low" ? 70 : (volMode == "High" ? 30 : 50);
             if (randCool < coolProb) { 
                 return new BrainDirective { DecisionType = "Random", TargetWinAmount = 0, Reason = "RTP Correction (High)" };
             }
        }

        // 6. Retention Hooks & Loss Streaks
        int skillOffset = profile.LuckyCloverLevel;
        int baseThreshold = volMode == "Low" ? 4 : (volMode == "High" ? 12 : 8);
        int retentionThreshold = Math.Max(2, (isBored ? baseThreshold / 2 : baseThreshold) - skillOffset);
        
        // Dynamic Threshold based on Global RTP: Higher RTP = Lower Threshold (More frequent help)
        if (globalRtp > 98.0m) retentionThreshold = Math.Max(2, retentionThreshold - 2);

        if ((profile.LossStreak >= retentionThreshold) || 
            (profile.TotalWagered > 50 && profile.ActualRtp < (double)((globalRtp - 15) / 100))) {
            
            decimal multMin = volMode == "Low" ? 2 : (volMode == "High" ? 20 : 5);
            decimal multMax = volMode == "Low" ? 10 : (volMode == "High" ? 100 : 25);
            
            decimal multiplier = (decimal)_rngService.GetNextInt(seedMain, 2, (int)multMin, (int)multMax);
            decimal targetWin = betAmount * multiplier;
            
            if (_vaultService.CanAffordWinCheck(userId, gameId, targetWin, repo)) {
                return new BrainDirective {
                    DecisionType = "RetentionHook",
                    TargetWinAmount = targetWin,
                    VolatilityModifier = volatility,
                    Reason = isBored ? "Boredom Recovery" : "Loss Streak Protection"
                };
            }
        }

        return new BrainDirective { 
            DecisionType = "Random", 
            VolatilityModifier = volatility 
        };
    }

    public void UpdateProfile(Guid userId, decimal betAmount, decimal winAmount, IGameRepository? repo = null) {
        if (repo != null) {
            UpdateProfileLogic(userId, betAmount, winAmount, repo);
        } else {
            using var scope = _scopeFactory.CreateScope();
            var scopedRepo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
            UpdateProfileLogic(userId, betAmount, winAmount, scopedRepo);
        }
    }

    private void UpdateProfileLogic(Guid userId, decimal betAmount, decimal winAmount, IGameRepository repo) {
        string cacheKey = $"user:profile:{userId}";
        var profile = _redisCache.GetAsync<PlayerProfile>(cacheKey).GetAwaiter().GetResult();

        if (profile == null) {
            profile = repo.GetPlayerProfile(userId);
        }

        if (profile == null) {
            profile = new PlayerProfile { 
                Id = Guid.NewGuid(), 
                UserId = userId,
                TotalWagered = 0,
                TotalPaid = 0,
                LastSpinTimestamp = DateTime.UtcNow
            };
            repo.CreatePlayerProfile(profile);
        }

        var now = DateTime.UtcNow;
        var interval = (now - profile.LastSpinTimestamp).TotalSeconds;
        if (interval > 0.5 && interval < 600) {
            profile.AvgSpinInterval = (profile.AvgSpinInterval * 0.8) + (interval * 0.2);
        }
        profile.LastSpinTimestamp = now;
        profile.TotalWagered += betAmount;
        profile.TotalPaid += winAmount;

        if (betAmount > 0 && winAmount > betAmount * 5) {
            var affinity = new Dictionary<int, double>();
            int seed = HashCode.Combine(userId, betAmount, winAmount, DateTime.UtcNow.Ticks);
            int favoriteCandidate = _rngService.GetNextInt(seed, 4, 1, 10); 
            if (!affinity.ContainsKey(favoriteCandidate)) affinity[favoriteCandidate] = 0;
            affinity[favoriteCandidate] += (double)winAmount / (double)betAmount;
            profile.SymbolAffinityJson = JsonSerializer.Serialize(affinity);
        }

        if (profile.TotalWagered > 0) {
            profile.ActualRtp = (double)(profile.TotalPaid / profile.TotalWagered);
        }

        if (winAmount > 0) {
            profile.LossStreak = 0;
        } else if (betAmount > 0) {
            profile.LossStreak++;
        }

        profile.LastUpdate = DateTime.UtcNow;
        repo.UpdatePlayerProfile(profile);
        
        // UPDATE CACHE instead of just invalidating
        _redisCache.SetAsync(cacheKey, profile, TimeSpan.FromMinutes(30)).GetAwaiter().GetResult();
    }
}