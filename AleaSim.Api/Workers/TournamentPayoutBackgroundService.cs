using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AleaSim.Api.Workers;

public class TournamentPayoutBackgroundService : BackgroundService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TournamentPayoutBackgroundService> _logger;

    public TournamentPayoutBackgroundService(IServiceScopeFactory scopeFactory, ILogger<TournamentPayoutBackgroundService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Tournament Payout Worker started.");

        while (!stoppingToken.IsCancellationRequested) {
            try {
                await CheckAndProcessPayouts();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error processing tournament payouts");
            }

            // Check every 1 hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task CheckAndProcessPayouts() {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var vault = scope.ServiceProvider.GetRequiredService<IVaultService>();
        var redis = scope.ServiceProvider.GetRequiredService<AleaSim.Domain.Services.IRedisCacheService>();
        var lockService = scope.ServiceProvider.GetRequiredService<ILockService>();

        var now = DateTime.UtcNow;
        var previousMonth = now.AddMonths(-1);
        string payoutKey = $"TournamentPaid_{previousMonth.Year}_{previousMonth.Month}";

        // If we are on the 1st day of the month and haven't paid out yet
        if (now.Day == 1) {
            // 1. Quick check in Redis
            var redisFlag = await redis.GetAsync<bool?>(payoutKey);
            if (redisFlag == true) return;

            // 2. Distributed Lock
            using var lockHandle = await lockService.AcquireLockAsync("tournament_payout_lock", TimeSpan.FromMinutes(10));
            
            // 3. Database Check + Transaction
            using var tx = repo.BeginTransaction();
            try {
                var dbFlag = repo.GetGlobalSetting(payoutKey);
                if (string.IsNullOrEmpty(dbFlag)) {
                    _logger.LogInformation($"Processing Tournament Payout for {previousMonth:MMMM yyyy}...");
                    
                    var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
                    await ProcessPayoutForMonth(previousMonth, repo, vault, audit);
                    
                    // Mark as paid in DB
                    repo.SetGlobalSetting(payoutKey, "true", $"Tournament Payout processed on {now}");
                    
                    // RESET POOL to base $25,000 for next month
                    repo.SetGlobalSetting("TournamentPrizePool", "25000.00", "Base pool for new month");
                    await redis.RemoveAsync("tournament:prize_pool");

                    tx.Commit();

                    // Update Redis for quick check
                    await redis.SetAsync(payoutKey, true, TimeSpan.FromDays(40));
                    _logger.LogInformation("Tournament Payout completed successfully.");
                } else {
                    tx.Rollback();
                }
            } catch (Exception ex) {
                tx.Rollback();
                _logger.LogError(ex, "Failed to process tournament payout transaction");
            }
        }
    }

    private async Task ProcessPayoutForMonth(DateTime month, IGameRepository repo, IVaultService vault, IAuditService audit) {
        var startOfMonth = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfMonth = startOfMonth.AddMonths(1).AddSeconds(-1);

        var topEntries = repo.GetTopTournamentEntries(endOfMonth, 10).ToList();
        if (!topEntries.Any()) return;

        // Use the persisted pool value
        decimal totalPool = 25000m;
        if (decimal.TryParse(repo.GetGlobalSetting("TournamentPrizePool"), out var dbVal)) {
            totalPool = dbVal;
        }

        // Distribution: 1st 40%, 2nd 25%, 3rd 15%, 4th-10th share 20%
        decimal[] distribution = { 0.40m, 0.25m, 0.15m, 0.05m, 0.03m, 0.03m, 0.03m, 0.02m, 0.02m, 0.02m };

        var winnersToSave = new List<TournamentWinner>();

        for (int i = 0; i < topEntries.Count && i < distribution.Length; i++) {
            var entry = topEntries[i];
            var user = repo.GetUser(entry.UserId);
            if (user == null) continue;

            decimal prize = totalPool * distribution[i];

            // Add prize to balance
            await vault.ProcessWinAsync(user.Id, prize, repo);

            winnersToSave.Add(new TournamentWinner {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Username = user.Username,
                AvatarUrl = user.AvatarUrl ?? "",
                PrizeAmount = prize,
                Rank = i + 1,
                Score = entry.RoiPercentage,
                Month = startOfMonth
            });
            
            audit.LogEvent("SYSTEM_UPDATE", $"Tournament payout for {month:MMM yyyy}: {user.Username} won {prize:C2}", "SYSTEM", "Tournament Payout");
        }

        if (winnersToSave.Any()) {
            repo.SaveTournamentWinners(winnersToSave);
        }
    }
}
