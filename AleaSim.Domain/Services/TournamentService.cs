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
        var now = DateTime.UtcNow;
        // Payout happens on the 1st day of the month for the PREVIOUS month
        if (now.Day != 1) return;

        string currentSeasonStr = repo.GetGlobalSetting("TournamentSeason") ?? "1";
        int currentSeason = int.Parse(currentSeasonStr);
        string payoutKey = $"TournamentPaid_S{currentSeason}";

        using var lockHandle = await _lockService.AcquireLockAsync("tournament_payout_master", TimeSpan.FromMinutes(10));
        
        // Double check flag after acquiring lock
        if (!string.IsNullOrEmpty(repo.GetGlobalSetting(payoutKey))) {
            _logger.LogInformation($"Tournament Season {currentSeason} already paid out.");
            return;
        }

        using var transaction = repo.BeginTransaction();
        try {
            _logger.LogInformation($"--- STARTING PAYOUT FOR SEASON {currentSeason} ---");
            
            // Get entries for the month that just ended
            var lastMonth = now.AddMonths(-1);
            var endOfLastMonth = new DateTime(now.Year, now.Month, 1).AddSeconds(-1);
            var entries = repo.GetTopTournamentEntries(endOfLastMonth, 10).ToList();

            decimal totalPool = 25000m;
            if (decimal.TryParse(repo.GetGlobalSetting("TournamentPrizePool"), out var dbVal)) {
                totalPool = dbVal;
            }

            if (!entries.Any()) {
                _logger.LogInformation($"No participants in Season {currentSeason}. Rolling over pool...");
                // Rollover logic: Prize pool stays for next month, maybe increases?
                // For now, we just proceed to next season.
            } else {
                decimal[] distribution = { 0.40m, 0.25m, 0.15m, 0.05m, 0.03m, 0.03m, 0.03m, 0.02m, 0.02m, 0.02m };
                var winnersToArchive = new List<TournamentWinner>();

                for (int i = 0; i < entries.Count && i < distribution.Length; i++) {
                    var entry = entries[i];
                    decimal prize = totalPool * distribution[i];
                    var user = repo.GetUser(entry.UserId);
                    if (user == null) continue;

                    // IDEMPOTENT PAYOUT: referenceId is unique per season and rank
                    var referenceId = Guid.Parse(SHA256Hash($"TOURN_S{currentSeason}_R{i+1}").Substring(0, 32));
                    
                    await vault.ProcessWinAsync(user.Id, prize, repo, referenceId);
                    
                    winnersToArchive.Add(new TournamentWinner {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        Username = user.Username,
                        AvatarUrl = user.AvatarUrl ?? "",
                        Rank = i + 1,
                        PrizeAmount = prize,
                        Score = entry.MaxMultiplier,
                        Month = new DateTime(lastMonth.Year, lastMonth.Month, 1)
                    });

                    await realTime.NotifyGameUpdate(user.Id, new {
                        Type = "TournamentWin",
                        Season = currentSeason,
                        Rank = i + 1,
                        Amount = prize,
                        Message = $"🏆 SEASON {currentSeason} FINISHED! You won {prize:C2}!"
                    });
                }
                repo.SaveTournamentWinners(winnersToArchive);
            }

            // --- AUTO ROTATION ---
            // 1. Mark Season as Paid
            repo.SetGlobalSetting(payoutKey, "true", $"Paid on {now}");
            
            // 2. Increment Season
            int nextSeason = currentSeason + 1;
            repo.SetGlobalSetting("TournamentSeason", nextSeason.ToString(), $"Started on {now}");
            
            // 3. Reset/Update Prize Pool (Base $25k + any carryover if desired)
            repo.SetGlobalSetting("TournamentPrizePool", "25000.00", "Starting pool for new season");
            
            repo.SaveChanges();
            transaction.Commit();
            
            _logger.LogInformation($"Season {currentSeason} finalized. Season {nextSeason} is now ACTIVE.");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "CRITICAL: Tournament Payout failed. Rolling back.");
            transaction.Rollback();
            throw;
        }
    }

    private string SHA256Hash(string input) {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
