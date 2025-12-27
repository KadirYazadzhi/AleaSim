using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IJackpotService {
    void Contribute(Guid gameId, decimal betAmount, IGameRepository repo);
    bool CheckJackpotTrigger(Guid gameId, int seed, int sequence, out decimal winAmount, IGameRepository repo);
    Jackpot GetGlobalJackpot(IGameRepository repo);
    Jackpot GetLocalJackpot(Guid gameId, IGameRepository repo);
}
