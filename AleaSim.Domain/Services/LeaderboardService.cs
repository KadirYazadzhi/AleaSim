using AleaSim.Domain.Interfaces;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace AleaSim.Domain.Services;

public class LeaderboardService : ILeaderboardService {
    private readonly IRealTimeService _realTimeService;
    private readonly IRedisService _redis;
    
    private const string KEY_HIGH_WINS = "leaderboard:daily_wins";
    private const string KEY_MULTIPLIERS = "leaderboard:big_multipliers";

    public LeaderboardService(IRealTimeService realTimeService, IRedisService redis) {
        _realTimeService = realTimeService;
        _redis = redis;
    }

    public void SubmitScore(Guid userId, string username, decimal winAmount, decimal betAmount, string gameName) {
        if (betAmount <= 0) return;

        decimal multiplier = winAmount / betAmount;
        var db = _redis.GetDatabase();

        // 1. Daily High Wins
        db.SortedSetAdd(KEY_HIGH_WINS, $"{userId}:{username}:{gameName}", (double)winAmount);

        // 2. Big Multipliers
        db.SortedSetAdd(KEY_MULTIPLIERS, $"{userId}:{username}:{gameName}", (double)multiplier);

        // Limit size to Top 100 in Redis to keep it clean
        db.SortedSetRemoveRangeByRank(KEY_HIGH_WINS, 0, -101);
        db.SortedSetRemoveRangeByRank(KEY_MULTIPLIERS, 0, -101);

        // 3. Global Big Win Broadcast (> 50x)
        if (multiplier >= 50) {
            _ = _realTimeService.NotifyBigWin(username, gameName, winAmount, multiplier);
        }

        // Notify real-time listeners (optional: throttle this)
        _ = NotifyUpdate();
    }

    private async Task NotifyUpdate() {
        var daily = GetLeaderboard("DailyHighWins").ToList();
        await _realTimeService.NotifyLeaderboardUpdate("DailyHighWins", daily);
        
        var big = GetLeaderboard("BigMultipliers").ToList();
        await _realTimeService.NotifyLeaderboardUpdate("BigMultipliers", big);
    }

    public IEnumerable<object> GetLeaderboard(string name) {
        var db = _redis.GetDatabase();
        string key = name == "BigMultipliers" ? KEY_MULTIPLIERS : KEY_HIGH_WINS;

        var entries = db.SortedSetRangeByRankWithScores(key, 0, 9, Order.Descending);
        
        return entries.Select(e => {
            var parts = e.Element.ToString().Split(':');
            return new LeaderboardEntry {
                UserId = Guid.Parse(parts[0]),
                Username = parts[1],
                GameName = parts.Length > 2 ? parts[2] : "Unknown",
                WinAmount = name == "BigMultipliers" ? 0 : (decimal)e.Score,
                Multiplier = name == "BigMultipliers" ? (decimal)e.Score : 0,
                Timestamp = DateTime.UtcNow
            };
        });
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
