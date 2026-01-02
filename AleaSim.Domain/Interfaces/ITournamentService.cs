using AleaSim.Domain.Entities;
using AleaSim.Shared.Models;

namespace AleaSim.Domain.Interfaces;

public interface ITournamentService {
    Task<IEnumerable<TournamentRankDto>> GetCurrentRankings(IGameRepository repo);
    Task ProcessMonthlyPayout(IGameRepository repo, IVaultService vault, IRealTimeService realTime);
}
