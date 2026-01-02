using AleaSim.Domain.Interfaces;
using AleaSim.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AleaSim.Domain.Services;

public class TournamentService : ITournamentService {
    private readonly ILogger<TournamentService> _logger;

    public TournamentService(ILogger<TournamentService> logger) {
        _logger = logger;
    }

    public async Task<IEnumerable<TournamentRankDto>> GetCurrentRankings(IGameRepository repo) {
        var date = DateTime.UtcNow;
        var entries = repo.GetTopTournamentEntries(date, 50); // Get top 50 candidates
        
        int rank = 1;
        var result = entries.Select(e => {
            var user = repo.GetUser(e.UserId);
            return new TournamentRankDto {
                Rank = rank++,
                UserId = e.UserId,
                Username = user?.Username ?? "Unknown",
                MaxMultiplier = e.TotalWagered > 0 ? (e.TotalPayout / e.TotalWagered) : 0, // Using simple multiplier for demo
                TotalPaid = e.TotalPayout
            };
        }).OrderByDescending(x => x.MaxMultiplier).ToList();

        // Re-rank after sorting
        for(int i=0; i<result.Count; i++) result[i].Rank = i + 1;

        return await Task.FromResult(result);
    }

    public async Task ProcessMonthlyPayout(IGameRepository repo, IVaultService vault, IRealTimeService realTime) {
        _logger.LogInformation("Processing Monthly Tournament Payouts...");
        
        var rankings = (await GetCurrentRankings(repo)).Take(10).ToList();
        decimal[] prizes = { 5000, 3000, 2000, 1500, 1000, 800, 700, 600, 500, 400 };

        foreach (var winner in rankings) {
            decimal prize = prizes[winner.Rank - 1];
            _logger.LogInformation("Winner #{Rank}: {User} gets {Amount}", winner.Rank, winner.Username, prize);
            
            vault.CreditBonus(winner.UserId, prize, prize, repo); // 1x Wagering
            
            await realTime.NotifyGameUpdate(winner.UserId, new {
                Type = "TournamentWin",
                Rank = winner.Rank,
                Amount = prize,
                Message = $"🏆 You finished #{winner.Rank} in the Tournament! ${prize} Bonus added!"
            });
        }
    }
}
