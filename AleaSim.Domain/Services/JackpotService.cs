using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using System.Collections.Concurrent;

namespace AleaSim.Domain.Services;

public class JackpotService : IJackpotService {
    private readonly ConcurrentDictionary<Guid, Jackpot> _localJackpots = new();
    private readonly Jackpot _globalJackpot = new() { 
        Id = Guid.NewGuid(), 
        Name = "Global Grand Jackpot", 
        CurrentValue = 10000m, 
        ContributionRate = 0.01m, // 1%
        IsGlobal = true,
        LastUpdated = DateTime.UtcNow
    };

    private readonly IRngService _rngService;

    public JackpotService(IRngService rngService) {
        _rngService = rngService;
    }

    public void Contribute(Guid gameId, decimal betAmount) {
        var local = _localJackpots.GetOrAdd(gameId, id => new Jackpot {
            Id = Guid.NewGuid(),
            GameId = id,
            Name = "Local Jackpot",
            CurrentValue = 500m,
            ContributionRate = 0.005m, // 0.5%
            IsGlobal = false,
            LastUpdated = DateTime.UtcNow
        });

        lock (_globalJackpot) {
            _globalJackpot.CurrentValue += betAmount * _globalJackpot.ContributionRate;
            _globalJackpot.LastUpdated = DateTime.UtcNow;
        }

        lock (local) {
            local.CurrentValue += betAmount * local.ContributionRate;
            local.LastUpdated = DateTime.UtcNow;
        }
    }

    public bool CheckJackpotTrigger(Guid gameId, int seed, int sequence, out decimal winAmount) {
        winAmount = 0;
        // Very low probability for jackpot
        double roll = _rngService.GetNextDouble(seed, HashCode.Combine(sequence, "jackpot"));
        
        if (roll < 0.0001) { // 1 in 10,000 chance for local
            var local = GetLocalJackpot(gameId);
            lock (local) {
                winAmount = local.CurrentValue;
                local.CurrentValue = 500m; // Reset to seed value
            }
            return true;
        }

        if (roll < 0.00001) { // 1 in 100,000 chance for global
            lock (_globalJackpot) {
                winAmount = _globalJackpot.CurrentValue;
                _globalJackpot.CurrentValue = 10000m; // Reset
            }
            return true;
        }

        return false;
    }

    public Jackpot GetGlobalJackpot() => _globalJackpot;

    public Jackpot GetLocalJackpot(Guid gameId) => _localJackpots.GetOrAdd(gameId, id => new Jackpot {
        Id = Guid.NewGuid(),
        GameId = id,
        Name = "Local Jackpot",
        CurrentValue = 500m,
        ContributionRate = 0.005m,
        IsGlobal = false,
        LastUpdated = DateTime.UtcNow
    });
}
