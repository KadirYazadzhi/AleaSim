using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Entities; // Added
using AleaSim.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AleaSim.Domain.Services;

public class TournamentService : ITournamentService {
    private readonly ILogger<TournamentService> _logger;
    private readonly ILockService _lockService;

    public TournamentService(ILogger<TournamentService> logger, ILockService lockService) {
        _logger = logger;
        _lockService = lockService;
    }

    public async Task<IEnumerable<TournamentRankDto>> GetCurrentRankings(IGameRepository repo) {
        var date = DateTime.UtcNow;
        var entries = repo.GetTopTournamentEntries(date, 50).ToList(); 
        
        // FIXED: Use MaxMultiplier field instead of calculating average
        var result = entries.Select((entry, index) => new TournamentRankDto {
            Rank = index + 1,
            UserId = entry.UserId,
            Username = entry.User?.Username ?? "Unknown",
            AvatarUrl = entry.User?.AvatarUrl ?? "https://api.dicebear.com/7.x/bottts/svg?seed=default",
            MaxMultiplier = entry.MaxMultiplier, // Use tracked max, not average RTP
            TotalPaid = entry.TotalPayout
        }).ToList();

        return await Task.FromResult(result);
    }

    public async Task ProcessMonthlyPayout(IGameRepository repo, IVaultService vault, IRealTimeService realTime) {
        using var lockHandle = await _lockService.AcquireLockAsync("tournament_payout", TimeSpan.FromMinutes(1));
        using var transaction = repo.BeginTransaction();
        
        try {
            _logger.LogInformation("Processing Monthly Tournament Payouts...");
            
            var rankings = (await GetCurrentRankings(repo)).Take(10).ToList();
            decimal[] prizes = { 5000, 3000, 2000, 1500, 1000, 800, 700, 600, 500, 400 };
            var winnersToArchive = new List<TournamentWinner>();

            foreach (var winner in rankings) {
                decimal prize = prizes[winner.Rank - 1];
                var user = repo.GetUser(winner.UserId);
                
                await vault.CreditBonusAsync(winner.UserId, prize, prize, repo); 
                
                winnersToArchive.Add(new TournamentWinner {
                    Id = Guid.NewGuid(),
                    UserId = winner.UserId,
                    Username = winner.Username,
                    AvatarUrl = user?.AvatarUrl ?? "",
                    Rank = winner.Rank,
                    PrizeAmount = prize,
                    Score = winner.MaxMultiplier,
                    Month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1)
                });

                await realTime.NotifyGameUpdate(winner.UserId, new {
                    Type = "TournamentWin",
                    Rank = winner.Rank,
                    Amount = prize,
                    Message = $"🏆 You finished #{winner.Rank} in the Tournament! ${prize} Bonus added!"
                });
            }

            repo.SaveTournamentWinners(winnersToArchive);
            repo.SaveChanges();
            transaction.Commit();
            
            _logger.LogInformation("Tournament payouts processed successfully for {Count} winners", winnersToArchive.Count);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to process tournament payouts, rolling back");
            transaction.Rollback();
            throw;
        }
    }
}
