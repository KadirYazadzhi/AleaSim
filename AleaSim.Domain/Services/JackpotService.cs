using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class JackpotService : IJackpotService {
    private readonly IRngService _rngService;
    private readonly IRealTimeService _realTimeService;
    private readonly ILockService _lockService;
    
    public JackpotService(IRngService rngService, IRealTimeService realTimeService, ILockService lockService) {
        _rngService = rngService;
        _realTimeService = realTimeService;
        _lockService = lockService;
    }

    public async Task Contribute(Guid gameId, decimal betAmount, IGameRepository repo) {
        using var lockHandle = await _lockService.AcquireLockAsync("global_jackpots", TimeSpan.FromSeconds(5));
        
        var jackpots = repo.GetJackpots().ToList();
        
        foreach (var j in jackpots) {
            bool shouldContribute = j.IsGlobal || (j.GameId == gameId);
            
            if (shouldContribute && (j.Tier == JackpotTier.Spades || j.Tier == JackpotTier.Hearts)) {
                j.CurrentValue += betAmount * j.ContributionRate;
                j.LastUpdated = DateTime.UtcNow;
                repo.UpdateJackpot(j);
                _ = _realTimeService.NotifyJackpotUpdate(j); 
            }
        }
    }

    public async Task<(bool Triggered, decimal WinAmount)> CheckJackpotTrigger(Guid gameId, int seed, int sequence, IGameRepository repo) {
        double roll = _rngService.GetNextDouble(seed, HashCode.Combine(sequence, "jackpot_trigger"));
        
        using var lockHandle = await _lockService.AcquireLockAsync("global_jackpots", TimeSpan.FromSeconds(5));
        
        // Only check jackpots relevant to this game
        var jackpots = repo.GetJackpots()
            .Where(j => j.IsGlobal || j.GameId == gameId)
            .OrderBy(j => j.Tier)
            .ToList();

        foreach (var j in jackpots) {
            decimal pressure = j.MustDropAt.HasValue ? j.CurrentValue / j.MustDropAt.Value : 0.1m;
            // Base chance varies by tier rarity
            double baseChance = j.Tier switch {
                JackpotTier.Clubs => 0.001,    // 1 in 1000
                JackpotTier.Diamonds => 0.0005, // 1 in 2000
                JackpotTier.Hearts => 0.0001,   // 1 in 10000
                JackpotTier.Spades => 0.00001,  // 1 in 100000
                _ => 0.0001
            };
            
            double threshold = baseChance * (double)pressure; 

            if (roll < threshold || (j.MustDropAt.HasValue && j.CurrentValue >= j.MustDropAt.Value)) {
                decimal win = j.CurrentValue;
                j.CurrentValue = GetResetValue(j.Tier);
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
        using var lockHandle = await _lockService.AcquireLockAsync("global_jackpots", TimeSpan.FromSeconds(5));
        var jackpot = repo.GetJackpots().FirstOrDefault(j => j.Tier == tier);
        if (jackpot == null) return 0m;

        decimal win = jackpot.CurrentValue;
        jackpot.CurrentValue = GetResetValue(tier);
        jackpot.LastUpdated = DateTime.UtcNow;
            
        repo.UpdateJackpot(jackpot);
        _ = _realTimeService.NotifyJackpotUpdate(jackpot);
            
        return win;
    }

    public decimal GetTierValue(JackpotTier tier, IGameRepository repo) {
        var jackpot = repo.GetJackpots().FirstOrDefault(j => j.Tier == tier);
        return jackpot?.CurrentValue ?? 0m;
    }
}