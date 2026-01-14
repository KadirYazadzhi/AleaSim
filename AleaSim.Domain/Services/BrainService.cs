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

    public BrainService(IServiceScopeFactory scopeFactory, IVaultService vaultService, IMemoryCache cache) {
        _scopeFactory = scopeFactory;
        _vaultService = vaultService;
        _cache = cache;
    }

    public BrainDirective GetNextDirective(Guid userId, Guid gameId, decimal betAmount, IGameRepository repo) {
        string forceKey = $"brain_force_{userId}";
        if (_cache.TryGetValue(forceKey, out BrainDirective? forced) && forced != null) {
            _cache.Remove(forceKey);
            return forced;
        }

        string queueKey = $"brain_queue_{userId}";
        var queue = _cache.GetOrCreate(queueKey, entry => {
            entry.SlidingExpiration = TimeSpan.FromMinutes(20); 
            return new Queue<BrainDirective>();
        });

        lock (queue!) {
            if (queue.Count == 0) {
                for (int i = 0; i < 5; i++) {
                    queue.Enqueue(DecideOutcome(userId, gameId, betAmount, repo));
                }
            }
            return queue.Dequeue();
        }
    }

    public void SetForcedDirective(Guid userId, BrainDirective directive) {
        _cache.Set($"brain_force_{userId}", directive, TimeSpan.FromHours(1));
    }

    public BrainDirective DecideOutcome(Guid userId, Guid gameId, decimal betAmount, IGameRepository repo, bool isShadowMode = false) {
        string forceKey = $"brain_force_{userId}";
        if (_cache.TryGetValue(forceKey, out BrainDirective? forced) && forced != null) {
            _cache.Remove(forceKey);
            return forced;
        }

        var profile = repo.GetPlayerProfile(userId); 
        if (profile == null) {
            return new BrainDirective { DecisionType = "Random" };
        }

        if (isShadowMode) {
            var rand = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
            if (rand < 30) { 
                return new BrainDirective { DecisionType = "NearMiss", IsNearMiss = true, TargetWinAmount = 0 };
            }
            return new BrainDirective { DecisionType = "Shadow_Random" };
        }

        // --- RULE 0: Flow State (Dynamic Difficulty) ---
        bool isInFlow = profile.AvgSpinInterval < 2.5;
        bool isBored = profile.AvgSpinInterval > 7.0;
        double volatility = isInFlow ? 2.0 : (isBored ? 0.5 : 1.0);

        // --- RULE 1: The Retention Hook ---
        int skillOffset = profile.LuckyCloverLevel;
        int retentionThreshold = Math.Max(2, (isBored ? 4 : 8) - skillOffset);
        
        if (profile.LossStreak >= retentionThreshold || (profile.CurrentSessionRtp < 0.5m && profile.TotalWagered > 50)) {
            decimal multiplier = isBored ? (decimal)(new Random().Next(2, 5)) : (decimal)(new Random().Next(10, 25));
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

        var randHighRtp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
        if (profile.ActualRtp > 2.5 && profile.TotalWagered > 100 && randHighRtp < 40) { 
            return new BrainDirective { DecisionType = "Random", TargetWinAmount = 0, Reason = "Cooling Down High RTP" };
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
        var profile = repo.GetPlayerProfile(userId);

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
            int favoriteCandidate = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 10); 
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
    }
}