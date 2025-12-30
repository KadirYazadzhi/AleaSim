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
            _ = _realTimeService.NotifyJackpotUpdate(global.Name, global.CurrentValue); // Fire and forget with notification

            var local = repo.GetOrCreateLocalJackpot(gameId);
            local.CurrentValue += betAmount * local.ContributionRate;
            local.LastUpdated = DateTime.UtcNow;
            repo.UpdateJackpot(local);
            _ = _realTimeService.NotifyJackpotUpdate(local.Name, local.CurrentValue); 
        }
        await Task.CompletedTask;
    }

    public async Task<(bool Triggered, decimal WinAmount)> CheckJackpotTrigger(Guid gameId, int seed, int sequence, IGameRepository repo) {
        decimal winAmount = 0;
        double roll = _rngService.GetNextDouble(seed, HashCode.Combine(sequence, "jackpot"));
        
        lock (_lock) {
            if (roll < 0.0001) { // Local Jackpot
                var local = repo.GetOrCreateLocalJackpot(gameId);
                winAmount = local.CurrentValue;
                local.CurrentValue = 500m; // Reset
                repo.UpdateJackpot(local);
                _ = _realTimeService.NotifyJackpotUpdate(local.Name, local.CurrentValue); 
                return (true, winAmount);
            }

            if (roll < 0.00001) { // Global Jackpot
                var global = repo.GetGlobalJackpot();
                winAmount = global.CurrentValue;
                global.CurrentValue = 10000m; // Reset
                repo.UpdateJackpot(global);
                _ = _realTimeService.NotifyJackpotUpdate(global.Name, global.CurrentValue); 
                return (true, winAmount);
            }
        }

        return (false, 0);
    }

    public Jackpot GetGlobalJackpot(IGameRepository repo) {
        return repo.GetGlobalJackpot();
    }

    public Jackpot GetLocalJackpot(Guid gameId, IGameRepository repo) {
        return repo.GetOrCreateLocalJackpot(gameId);
    }
}