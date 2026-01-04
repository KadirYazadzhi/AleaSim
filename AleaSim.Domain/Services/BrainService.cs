using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Collections.Concurrent; // Added

namespace AleaSim.Domain.Services;

public class BrainService : IBrainService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVaultService _vaultService;
    private readonly ConcurrentDictionary<Guid, BrainDirective> _forcedDirectives = new(); 
    private readonly ConcurrentDictionary<Guid, Queue<BrainDirective>> _directiveQueues = new(); // Added

    public BrainService(IServiceScopeFactory scopeFactory, IVaultService vaultService) {
        _scopeFactory = scopeFactory;
        _vaultService = vaultService;
    }

    public BrainDirective GetNextDirective(Guid userId, Guid gameId, decimal betAmount, IGameRepository repo) {
        // 1. Admin Forced always wins
        if (_forcedDirectives.TryRemove(userId, out var forced)) return forced;

        // 2. Get or Init Queue
        var queue = _directiveQueues.GetOrAdd(userId, _ => new Queue<BrainDirective>());

        lock (queue) {
            if (queue.Count == 0) {
                // Pre-calculate 5 steps
                for (int i = 0; i < 5; i++) {
                    queue.Enqueue(DecideOutcome(userId, gameId, betAmount, repo));
                }
            }
            return queue.Dequeue();
        }
    }

    public void SetForcedDirective(Guid userId, BrainDirective directive) {
        _forcedDirectives[userId] = directive;
    }

    public BrainDirective DecideOutcome(Guid userId, Guid gameId, decimal betAmount, IGameRepository repo, bool isShadowMode = false) {
        // 0. Admin Override
        if (_forcedDirectives.TryRemove(userId, out var forced)) {
            return forced;
        }

        var profile = repo.GetPlayerProfile(userId); 
        if (profile == null) {
            return new BrainDirective { DecisionType = "Random" };
        }

        // In Shadow Mode, we simulate a different "Generous" algorithm for testing
        if (isShadowMode) {
            if (new Random().NextDouble() < 0.3) {
                return new BrainDirective { 
                    DecisionType = "Shadow_GenerousWin", 
                    TargetWinAmount = betAmount * 5,
                    Reason = "Testing Generous Algorithm" 
                };
            }
            return new BrainDirective { DecisionType = "Shadow_Random" };
        }

        // --- RULE 0: Flow State (Dynamic Difficulty) ---
        bool isInFlow = profile.AvgSpinInterval < 2.5;
        bool isBored = profile.AvgSpinInterval > 7.0;
        double volatility = isInFlow ? 2.0 : (isBored ? 0.5 : 1.0);

        // --- RULE 1: The Retention Hook ---
        // Adjusted by Flow: If bored, trigger hook earlier
        int skillOffset = profile.LuckyCloverLevel;
        int retentionThreshold = Math.Max(2, (isBored ? 4 : 8) - skillOffset);
        
        if (profile.LossStreak >= retentionThreshold || (profile.CurrentSessionRtp < 0.5m && profile.TotalWagered > 50)) {
            decimal multiplier = isBored ? (decimal)(new Random().Next(2, 5)) : (decimal)(new Random().Next(10, 25));
            decimal targetWin = betAmount * multiplier;
            
            if (_vaultService.CanAffordWin(userId, gameId, targetWin, repo)) {
                return new BrainDirective {
                    DecisionType = "RetentionHook",
                    TargetWinAmount = targetWin,
                    VolatilityModifier = volatility,
                    Reason = isBored ? "Boredom Recovery" : "Loss Streak Protection"
                };
            }
        }

        // --- RULE 2: The Cool Down ---
        if (profile.ActualRtp > 1.2 && profile.TotalWagered > 100) {
            // Find favorite symbol for Teaser from affinity data
            int favoriteSymbol = 7; // Default to Seven
            try {
                var affinity = JsonSerializer.Deserialize<Dictionary<int, double>>(profile.SymbolAffinityJson);
                if (affinity != null && affinity.Any()) {
                    favoriteSymbol = affinity.OrderByDescending(x => x.Value).First().Key;
                }
            } catch { }

            return new BrainDirective {
                DecisionType = "CoolDown",
                TargetWinAmount = 0,
                IsNearMiss = true,
                PreferredNearMissSymbol = favoriteSymbol,
                VolatilityModifier = volatility,
                Reason = "pRTP normalization"
            };
        }

        // Default with Volatility
        return new BrainDirective { 
            DecisionType = "Random", 
            VolatilityModifier = volatility 
        };
    }

    public void UpdateProfile(Guid userId, decimal betAmount, decimal winAmount) {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
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

        // Flow State Calculation
        var now = DateTime.UtcNow;
        var interval = (now - profile.LastSpinTimestamp).TotalSeconds;
        // Exponential Moving Average (Alpha = 0.2) to smooth out spikes
        if (interval > 0.5 && interval < 600) { // Ignore impossible speeds or AFK
            profile.AvgSpinInterval = (profile.AvgSpinInterval * 0.8) + (interval * 0.2);
        }
        profile.LastSpinTimestamp = now;

        profile.TotalWagered += betAmount;
        profile.TotalPaid += winAmount;

        // Affinity Tracking: If user won, boost the affinity of high-value symbols
        // For now, we simulate this by looking at win size.
        // In a real system, we'd pass the winning symbol ID here.
        if (winAmount > betAmount * 5) {
            var affinity = JsonSerializer.Deserialize<Dictionary<int, double>>(profile.SymbolAffinityJson) ?? new();
            // In CloverChase: 7 and Clover (8) are favorites.
            // Let's assume for this prototype we boost 7 or 8 based on a simple logic.
            int favoriteCandidate = (new Random().NextDouble() > 0.5) ? 7 : 8; 
            if (!affinity.ContainsKey(favoriteCandidate)) affinity[favoriteCandidate] = 0;
            affinity[favoriteCandidate] += (double)(winAmount / betAmount);
            profile.SymbolAffinityJson = JsonSerializer.Serialize(affinity);
        }
        
        if (profile.TotalWagered > 0) {
            profile.ActualRtp = (double)(profile.TotalPaid / profile.TotalWagered);
        }

        if (winAmount > 0) {
            profile.LossStreak = 0;
            profile.CurrentSessionRtp = (decimal)((double)winAmount / (double)betAmount); // Simplification for session
        } else {
            profile.LossStreak++;
        }

        profile.LastUpdate = DateTime.UtcNow;
        repo.UpdatePlayerProfile(profile);
    }
}
