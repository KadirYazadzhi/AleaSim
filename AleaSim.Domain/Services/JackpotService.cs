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
            var global = repo.GetGlobalJackpot();
            global.CurrentValue += betAmount * global.ContributionRate;
            global.LastUpdated = DateTime.UtcNow;
            repo.UpdateJackpot(global);
            _ = _realTimeService.NotifyJackpotUpdate(global); 

            var local = repo.GetOrCreateLocalJackpot(gameId);
            local.CurrentValue += betAmount * local.ContributionRate;
            local.LastUpdated = DateTime.UtcNow;
            repo.UpdateJackpot(local);
            _ = _realTimeService.NotifyJackpotUpdate(local); 
        }
        await Task.CompletedTask;
    }

    public async Task<(bool Triggered, decimal WinAmount)> CheckJackpotTrigger(Guid gameId, int seed, int sequence, IGameRepository repo) {
        decimal winAmount = 0;
        double roll = _rngService.GetNextDouble(seed, HashCode.Combine(sequence, "jackpot"));
        
        lock (_lock) {
            // Check Local Jackpot with Pressure
            var local = repo.GetOrCreateLocalJackpot(gameId);
            if (ShouldTrigger(local, roll, 0.0001)) { // Base chance 1 in 10,000
                winAmount = local.CurrentValue;
                local.CurrentValue = 500m; // Reset base
                repo.UpdateJackpot(local);
                _ = _realTimeService.NotifyJackpotUpdate(local); 
                return (true, winAmount);
            }

            // Check Global Jackpot with Pressure
            var global = repo.GetGlobalJackpot();
            if (ShouldTrigger(global, roll, 0.00001)) { // Base chance 1 in 100,000
                winAmount = global.CurrentValue;
                global.CurrentValue = 10000m; // Reset base
                repo.UpdateJackpot(global);
                _ = _realTimeService.NotifyJackpotUpdate(global); 
                return (true, winAmount);
            }
        }

        return (false, 0);
    }

    private bool ShouldTrigger(Jackpot jackpot, double roll, double baseChance) {
        // 1. Force Drop if cap reached
        if (jackpot.MustDropAt.HasValue && jackpot.CurrentValue >= jackpot.MustDropAt.Value) {
            return true;
        }

        // 2. Standard RNG if no cap
        if (!jackpot.MustDropAt.HasValue) {
            return roll < baseChance;
        }

        // 3. Pressure Logic
        decimal pressure = jackpot.CurrentValue / jackpot.MustDropAt.Value;
        
        if (pressure < 0.9m) {
            return roll < baseChance; // Normal luck
        }
        else {
            // "Hot Zone" (90% to 100%)
            // Increase chance exponentially as we get closer to 1.0
            // Example: Pressure 0.95 -> 1 / (1 - 0.95) = 20x multiplier
            // Pressure 0.99 -> 1 / (1 - 0.99) = 100x multiplier
            double multiplier = (double)(1m / (1m - pressure));
            // Cap multiplier to avoid infinity (though cap check above handles >= 1.0)
            if (multiplier > 1000) multiplier = 1000;
            
            return roll < (baseChance * multiplier);
        }
    }

    public Jackpot GetGlobalJackpot(IGameRepository repo) {
        return repo.GetGlobalJackpot();
    }

    public Jackpot GetLocalJackpot(Guid gameId, IGameRepository repo) {
        return repo.GetOrCreateLocalJackpot(gameId);
    }
}