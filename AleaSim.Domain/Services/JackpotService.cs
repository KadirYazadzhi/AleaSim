using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class JackpotService : IJackpotService {
    private readonly IRngService _rngService;
    private readonly IRealTimeService _realTimeService;
    private readonly object _lock = new();
    
    public JackpotService(IRngService rngService, IRealTimeService realTimeService) {
        _rngService = rngService;
        _realTimeService = realTimeService;
    }

    public async Task Contribute(Guid gameId, decimal betAmount, IGameRepository repo) {
        lock (_lock) {
            var jackpots = repo.GetJackpots().ToList();
            foreach (var j in jackpots) {
                j.CurrentValue += betAmount * j.ContributionRate;
                j.LastUpdated = DateTime.UtcNow;
                repo.UpdateJackpot(j);
                _ = _realTimeService.NotifyJackpotUpdate(j); 
            }
        }
        await Task.CompletedTask;
    }

    public async Task<(bool Triggered, decimal WinAmount)> CheckJackpotTrigger(Guid gameId, int seed, int sequence, IGameRepository repo) {
        double roll = _rngService.GetNextDouble(seed, HashCode.Combine(sequence, "jackpot_trigger"));
        
        lock (_lock) {
            var jackpots = repo.GetJackpots().OrderBy(j => j.Tier).ToList();
            foreach (var j in jackpots) {
                // Must Drop Pressure Logic
                decimal pressure = j.MustDropAt.HasValue ? j.CurrentValue / j.MustDropAt.Value : 0.1m;
                double threshold = 0.0001 * (double)pressure; // Base 1 in 10,000 * pressure

                if (roll < threshold || (j.MustDropAt.HasValue && j.CurrentValue >= j.MustDropAt.Value)) {
                    decimal win = j.CurrentValue;
                    j.CurrentValue = GetResetValue(j.Tier);
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
        JackpotTier.Clubs => 10m,
        JackpotTier.Diamonds => 50m,
        JackpotTier.Hearts => 200m,
        JackpotTier.Spades => 5000m,
        _ => 0m
    };

    public Jackpot GetGlobalJackpot(IGameRepository repo) => repo.GetJackpots().First(j => j.Tier == JackpotTier.Spades);
    public Jackpot GetLocalJackpot(Guid gameId, IGameRepository repo) => repo.GetOrCreateLocalJackpot(gameId);
}