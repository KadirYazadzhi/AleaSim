using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Interfaces;

public interface IQuestService {
    Task UpdateProgressAsync(Guid userId, string goalType, decimal value, IGameRepository repo, IRealTimeService realTime, IVaultService vault);
    Task GenerateDailyQuests(Guid userId, IGameRepository repo);
    Task<IEnumerable<UserQuestProgress>> GetActiveQuests(Guid userId, IGameRepository repo);
}
