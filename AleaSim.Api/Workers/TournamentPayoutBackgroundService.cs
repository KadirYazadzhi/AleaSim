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

        var now = DateTime.UtcNow;
        var previousMonth = now.AddMonths(-1);
        string payoutKey = $"tournament_payout_{previousMonth.Year}_{previousMonth.Month}";

        // If we are on the 1st day of the month and haven't paid out yet
        if (now.Day == 1) {
            var alreadyPaid = await redis.GetAsync<bool?>(payoutKey);
            
            if (alreadyPaid == null || !alreadyPaid.Value) {
                // Ensure only one server instance processes this by acquiring a lock
                var lockService = scope.ServiceProvider.GetRequiredService<ILockService>();
                using var lockHandle = await lockService.AcquireLockAsync("tournament_payout_lock", TimeSpan.FromMinutes(5));
                
                // Double check after lock
                alreadyPaid = await redis.GetAsync<bool?>(payoutKey);
                if (alreadyPaid == null || !alreadyPaid.Value) {
                    _logger.LogInformation($"Processing Tournament Payout for {previousMonth.ToString("MMMM yyyy")}...");
                    
                    var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
                    await ProcessPayoutForMonth(previousMonth, repo, vault, audit);
                    
                    await redis.SetAsync(payoutKey, true, TimeSpan.FromDays(40)); // Keep flag for 40 days
                    _logger.LogInformation("Tournament Payout completed successfully.");
                }
            }
        }
    }

    private async Task ProcessPayoutForMonth(DateTime month, IGameRepository repo, IVaultService vault, IAuditService audit) {
        // Calculate Pool (Base 25000 + 1% of total wagers)
        var startOfMonth = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfMonth = startOfMonth.AddMonths(1).AddSeconds(-1);

        // Calculate total wagers for the month using context if possible, or assume a fixed logic if complex.
        // We will just use the repository's Top entries which we can fetch.
        var topEntries = repo.GetTopTournamentEntries(endOfMonth, 10).ToList();

        if (!topEntries.Any()) return;

        // Since we don't have a direct repo method for monthly total wagers, we'll estimate or use base.
        // For production, a dedicated GetMonthlyWagers method should be used.
        decimal basePool = 25000m;
        decimal totalPool = basePool; // Simplified for now. 

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
                Score = entry.Score,
                Month = startOfMonth
            });
            
            audit.LogEvent("SYSTEM_UPDATE", $"Tournament payout for {month:MMM yyyy}: {user.Username} won {prize:C}", "SYSTEM", "Tournament Payout");
        }

        if (winnersToSave.Any()) {
            repo.SaveTournamentWinners(winnersToSave);
        }
    }

}
