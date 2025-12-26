using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class JackpotService : IJackpotService {
    private readonly IGameRepository _repository;
    private readonly IRngService _rngService;
    
    public JackpotService(IRngService rngService, IGameRepository repository) {
        _rngService = rngService;
        _repository = repository;
        // InitializeGlobalJackpot() is handled by repo getter if missing
    }

    public void Contribute(Guid gameId, decimal betAmount) {
        var global = _repository.GetGlobalJackpot();
        global.CurrentValue += betAmount * global.ContributionRate;
        global.LastUpdated = DateTime.UtcNow;
        _repository.UpdateJackpot(global);

        var local = _repository.GetOrCreateLocalJackpot(gameId);
        local.CurrentValue += betAmount * local.ContributionRate;
        local.LastUpdated = DateTime.UtcNow;
        _repository.UpdateJackpot(local);
    }

    public bool CheckJackpotTrigger(Guid gameId, int seed, int sequence, out decimal winAmount) {
        winAmount = 0;
        double roll = _rngService.GetNextDouble(seed, HashCode.Combine(sequence, "jackpot"));
        
        if (roll < 0.0001) { // Local Jackpot
            var local = _repository.GetOrCreateLocalJackpot(gameId);
            winAmount = local.CurrentValue;
            local.CurrentValue = 500m; // Reset
            _repository.UpdateJackpot(local);
            return true;
        }

        if (roll < 0.00001) { // Global Jackpot
            var global = _repository.GetGlobalJackpot();
            winAmount = global.CurrentValue;
            global.CurrentValue = 10000m; // Reset
            _repository.UpdateJackpot(global);
            return true;
        }

        return false;
    }

    public Jackpot GetGlobalJackpot() {
        return _repository.GetGlobalJackpot();
    }

    public Jackpot GetLocalJackpot(Guid gameId) {
        return _repository.GetOrCreateLocalJackpot(gameId);
    }
}
