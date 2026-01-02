using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AleaSim.Domain.Services;

public class BrainService : IBrainService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVaultService _vaultService;

    public BrainService(IServiceScopeFactory scopeFactory, IVaultService vaultService) {
        _scopeFactory = scopeFactory;
        _vaultService = vaultService;
    }

    public BrainDirective DecideOutcome(Guid userId, Guid gameId, decimal betAmount, bool isShadowMode = false) {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        
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
        // Fast Play (< 2.5s) -> High Volatility (Big Wins or Nothing)
        // Slow Play (> 7.0s) -> Low Volatility (Frequent Small Wins)
        bool isInFlow = profile.AvgSpinInterval < 2.5;
        bool isBored = profile.AvgSpinInterval > 7.0;

        // --- RULE 1: The Retention Hook ---
        // Adjusted by Flow: If bored, trigger hook earlier
        int retentionThreshold = isBored ? 4 : 8;
        
        if (profile.LossStreak >= retentionThreshold || (profile.CurrentSessionRtp < 0.5m && profile.TotalWagered > 50)) {
            decimal multiplier = isBored ? (decimal)(new Random().Next(2, 5)) : (decimal)(new Random().Next(10, 25));
            decimal targetWin = betAmount * multiplier;
            
            if (_vaultService.CanAffordWin(userId, gameId, targetWin, repo)) {
                return new BrainDirective {
                    DecisionType = "RetentionHook",
                    TargetWinAmount = targetWin,
                    Reason = isBored ? "Boredom Recovery" : "Loss Streak Protection"
                };
            }
        }

        // --- RULE 2: Flow State Volatility Modification ---
        if (isInFlow) {
            // In Flow: We want big hits. If a random small win was going to happen, 
            // there's a 70% chance we convert it to a Loss to save for a big one later.
            if (new Random().NextDouble() < 0.7) {
                return new BrainDirective { 
                    DecisionType = "FlowVolatility", 
                    TargetWinAmount = 0, 
                    IsNearMiss = true,
                    Reason = "High Speed Volatility Shift" 
                };
            }
        }
        else if (isBored) {
            // Bored: Force a small "Drip" win (1.5x - 3x) even if RNG said Loss
            if (profile.LossStreak > 2 && new Random().NextDouble() < 0.4) {
                decimal dripWin = betAmount * (decimal)(1.5 + new Random().NextDouble() * 1.5);
                if (_vaultService.CanAffordWin(userId, gameId, dripWin, repo)) {
                    return new BrainDirective {
                        DecisionType = "BoredomDrip",
                        TargetWinAmount = dripWin,
                        Reason = "Engagement Boost"
                    };
                }
            }
        }

        // --- RULE 2: The Cool Down (Stop them from winning too much) ---
        // If user is winning too much (pRTP > 150%)
        if (profile.ActualRtp > 1.5 && profile.TotalWagered > 100) {
            // Force a Near Miss (Teaser)
            return new BrainDirective {
                DecisionType = "CoolDown",
                TargetWinAmount = 0,
                IsNearMiss = true,
                Reason = "pRTP too high"
            };
        }

        // --- RULE 3: The Whale Protocol (High Rollers) ---
        if (betAmount >= 50) {
            // High Rollers hate small wins (1x). Give them nothing or Big Win.
            // 80% chance of Loss, 20% chance of Big Win check
            if (new Random().NextDouble() < 0.2) {
                 decimal whaleWin = betAmount * 20;
                 if (_vaultService.CanAffordWin(userId, gameId, whaleWin, repo)) {
                     return new BrainDirective {
                        DecisionType = "WhaleBonus",
                        TargetWinAmount = whaleWin
                    };
                 }
            }
            // Else force loss (don't give small wins)
            return new BrainDirective { DecisionType = "WhaleLoss", TargetWinAmount = 0 };
        }

        // Default: Let the engine decide (Random within standard RTP)
        return new BrainDirective { DecisionType = "Random" };
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
        profile.NetDeposit = profile.TotalWagered - profile.TotalPaid;
        
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
