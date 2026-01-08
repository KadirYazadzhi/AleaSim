using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IJackpotService {
    Task Contribute(Guid gameId, decimal betAmount, IGameRepository repo);
    Task<(bool Triggered, decimal WinAmount)> CheckJackpotTrigger(Guid gameId, int seed, int sequence, IGameRepository repo);
    Jackpot GetGlobalJackpot(IGameRepository repo);
    Jackpot GetLocalJackpot(Guid gameId, IGameRepository repo);
    decimal GetTierValue(JackpotTier tier, IGameRepository repo); // Added
    decimal ClaimJackpot(JackpotTier tier, IGameRepository repo);
}
