using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class JackpotService : IJackpotService {
    private readonly IRngService _rngService;
    private readonly IRealTimeService _realTimeService; // Added
    private readonly object _lock = new();
    
    public JackpotService(IRngService rngService, IRealTimeService realTimeService) {
        _rngService = rngService;
        _realTimeService = realTimeService;
    }

    public void Contribute(Guid gameId, decimal betAmount, IGameRepository repo) {
        lock (_lock) {
            var global = repo.GetGlobalJackpot();
            global.CurrentValue += betAmount * global.ContributionRate;
            global.LastUpdated = DateTime.UtcNow;
            repo.UpdateJackpot(global);
            _realTimeService.NotifyJackpotUpdate(global.Name, global.CurrentValue); // Notify

            var local = repo.GetOrCreateLocalJackpot(gameId);
            local.CurrentValue += betAmount * local.ContributionRate;
            local.LastUpdated = DateTime.UtcNow;
            repo.UpdateJackpot(local);
            _realTimeService.NotifyJackpotUpdate(local.Name, local.CurrentValue); // Notify
        }
    }

    public bool CheckJackpotTrigger(Guid gameId, int seed, int sequence, out decimal winAmount, IGameRepository repo) {
        winAmount = 0;
        double roll = _rngService.GetNextDouble(seed, HashCode.Combine(sequence, "jackpot"));
        
        lock (_lock) {
            if (roll < 0.0001) { // Local Jackpot
                var local = repo.GetOrCreateLocalJackpot(gameId);
                winAmount = local.CurrentValue;
                local.CurrentValue = 500m; // Reset
                repo.UpdateJackpot(local);
                _realTimeService.NotifyJackpotUpdate(local.Name, local.CurrentValue); // Notify reset
                return true;
            }

            if (roll < 0.00001) { // Global Jackpot
                var global = repo.GetGlobalJackpot();
                winAmount = global.CurrentValue;
                global.CurrentValue = 10000m; // Reset
                repo.UpdateJackpot(global);
                _realTimeService.NotifyJackpotUpdate(global.Name, global.CurrentValue); // Notify reset
                return true;
            }
        }

        return false;
    }

    public Jackpot GetGlobalJackpot(IGameRepository repo) {
        return repo.GetGlobalJackpot();
    }

    public Jackpot GetLocalJackpot(Guid gameId, IGameRepository repo) {
        return repo.GetOrCreateLocalJackpot(gameId);
    }
}
