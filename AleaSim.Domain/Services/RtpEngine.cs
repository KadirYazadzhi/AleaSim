using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class RtpEngine : IRtpEngine {
    private readonly object _lock = new();
    private readonly IRealTimeService _realTimeService;

    public RtpEngine(IRealTimeService realTimeService) {
        _realTimeService = realTimeService;
    }

    public async Task<bool> ProcessWin(Guid gameId, Guid userId, decimal winAmount, decimal betAmount, IGameRepository repo) {
        lock (_lock) {
            var game = repo.GetGame(gameId);
            if (game == null) return false;

            if (game.PoolBalance >= winAmount) {
                game.PoolBalance -= winAmount;
                repo.UpdateGame(game);
                repo.UpdateRtpStats(gameId, userId, 0, winAmount);
                return true; 
            }
            else {
                return false; 
            }
        }
    }

    public async Task RecordBet(Guid gameId, Guid userId, decimal amount, IGameRepository repo) {
        lock (_lock) {
            var game = repo.GetGame(gameId);
            if (game == null) return;

            decimal contribution = amount * (decimal)game.TargetRTP;
            
            game.PoolBalance += contribution;
            repo.UpdateGame(game);

            repo.UpdateRtpStats(gameId, userId, amount, 0);

             var stats = repo.GetOrCreateGameStats(gameId);
             if (stats.TotalWagered > 0)
                _ = _realTimeService.NotifyRtpUpdate(gameId, (double)(stats.TotalPaid / stats.TotalWagered));
        }
        await Task.CompletedTask;
    }

    public RTPStatistics GetGameStats(Guid gameId, IGameRepository repo) {
        return repo.GetOrCreateGameStats(gameId);
    }

    public RTPStatistics GetUserStats(Guid userId, IGameRepository repo) {
        return repo.GetOrCreateUserStats(userId);
    }
}