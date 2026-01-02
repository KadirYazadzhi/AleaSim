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

    public BrainDirective DecideOutcome(Guid userId, Guid gameId, decimal betAmount) {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        
        var profile = repo.GetPlayerProfile(userId); // We need to add this method to Repo later
        if (profile == null) {
            // New player, no profile yet. Default to Random.
            return new BrainDirective { DecisionType = "Random" };
        }

        // --- RULE 0: Flow State (Dynamic Difficulty) ---
        // Fast Play (< 2s) -> High Volatility (Big Wins or Nothing)
        // Slow Play (> 8s) -> Low Volatility (Frequent Small Wins)
        if (profile.AvgSpinInterval < 2.0) {
            // Player is in "The Zone". Don't interrupt with small wins.
            // Increase volatility: Reject small wins (< 2x)
            // This is a "Soft Directive" - we might modify random outcome logic later
             // For now, let's just Log it or bias slightly?
             // Let's implement bias: If Brain was going to be Random, force a choice?
             // No, let's keep it simple: If Fast, Brain prefers "WhaleLoss" or "WhaleBonus" (High Volatility)
        }

        // --- RULE 1: The Retention Hook (Stop them from leaving) ---
        // If user lost > 5 times in a row OR session RTP is terrible (< 50%)
        // Adjusted by Flow: If slow (bored), trigger hook earlier (LossStreak > 4)
        int retentionThreshold = (profile.AvgSpinInterval > 8.0) ? 4 : 8;
        
        if (profile.LossStreak >= retentionThreshold || (profile.CurrentSessionRtp < 0.5m && profile.TotalWagered > 50)) {
            // Force a win of 5x - 10x bet
            decimal targetWin = betAmount * (decimal)(new Random().Next(5, 10));
            
            // Validation: Can Vault afford this "Bribe"?
            if (_vaultService.CanAffordWin(userId, gameId, targetWin, repo)) {
                return new BrainDirective {
                    DecisionType = "RetentionHook",
                    TargetWinAmount = targetWin,
                    Reason = "User Loss Streak High"
                };
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
