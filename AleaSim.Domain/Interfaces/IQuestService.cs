using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Interfaces;

public interface IQuestService {
    Task UpdateProgress(Guid userId, string goalType, decimal value, IGameRepository repo, IRealTimeService realTime, IVaultService vault);
    Task<IEnumerable<UserQuestProgress>> GetActiveQuests(Guid userId, IGameRepository repo);
}
