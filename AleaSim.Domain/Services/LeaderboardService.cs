using AleaSim.Domain.Interfaces;
using System.Collections.Concurrent;

namespace AleaSim.Domain.Services;

public class LeaderboardService : ILeaderboardService {
    private readonly IRealTimeService _realTimeService;
    
    // In-Memory Storage: Name -> List of Entries
    private readonly ConcurrentDictionary<string, List<LeaderboardEntry>> _leaderboards = new();

    public LeaderboardService(IRealTimeService realTimeService) {
        _realTimeService = realTimeService;
        InitializeLeaderboards();
    }

    private void InitializeLeaderboards() {
        _leaderboards["DailyHighWins"] = new List<LeaderboardEntry>();
        _leaderboards["BigMultipliers"] = new List<LeaderboardEntry>();
    }

    public void SubmitScore(Guid userId, string username, decimal winAmount, decimal betAmount, string gameName) {
        if (betAmount <= 0) return;

        decimal multiplier = winAmount / betAmount;
        var entry = new LeaderboardEntry {
            UserId = userId,
            Username = username,
            WinAmount = winAmount,
            Multiplier = multiplier,
            GameName = gameName,
            Timestamp = DateTime.UtcNow
        };

        bool updated = false;

        // 1. Daily High Wins (Absolute Value)
        updated |= UpdateLeaderboard("DailyHighWins", entry, (a, b) => a.WinAmount.CompareTo(b.WinAmount));

        // 2. Big Multipliers (Luck Factor)
        updated |= UpdateLeaderboard("BigMultipliers", entry, (a, b) => a.Multiplier.CompareTo(b.Multiplier));

        // 3. Global Big Win Broadcast (> 50x)
        if (multiplier >= 50) {
            _ = _realTimeService.NotifyBigWin(username, gameName, winAmount, multiplier);
        }
    }

    private bool UpdateLeaderboard(string name, LeaderboardEntry entry, Comparison<LeaderboardEntry> comparer) {
        if (!_leaderboards.ContainsKey(name)) return false;

        lock (_leaderboards[name]) {
            var list = _leaderboards[name];
            
            // Check if user is already in list with a LOWER score
            var existing = list.FirstOrDefault(x => x.UserId == entry.UserId);
            if (existing != null) {
                // If new score is lower, ignore
                if (comparer(entry, existing) <= 0) return false;
                list.Remove(existing);
            }

            list.Add(entry);
            list.Sort((a, b) => comparer(b, a)); // Descending
            
            // Keep Top 10
            if (list.Count > 10) {
                list.RemoveAt(list.Count - 1);
            }

            // Only notify if this entry made it to Top 10
            if (list.Contains(entry)) {
                // Optimize: Don't spam notifications on every update, throttle in real world.
                // For demo: Notify immediately.
                _ = _realTimeService.NotifyLeaderboardUpdate(name, list);
                return true;
            }
        }
        return false;
    }

    public IEnumerable<object> GetLeaderboard(string name) {
        if (_leaderboards.TryGetValue(name, out var list)) {
            lock (list) {
                return list.ToList(); // Return copy
            }
        }
        return Enumerable.Empty<object>();
    }

    private class LeaderboardEntry {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public decimal WinAmount { get; set; }
        public decimal Multiplier { get; set; }
        public string GameName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
