using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IJackpotService {
    void Contribute(Guid gameId, decimal betAmount);
    bool CheckJackpotTrigger(Guid gameId, int seed, int sequence, out decimal winAmount);
    Jackpot GetGlobalJackpot();
    Jackpot GetLocalJackpot(Guid gameId);
}
