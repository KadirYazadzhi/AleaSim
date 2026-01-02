using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Interfaces;

public interface ILeaderboardService {
    void SubmitScore(Guid userId, string username, decimal winAmount, decimal betAmount, string gameName);
    IEnumerable<object> GetLeaderboard(string name);
}
