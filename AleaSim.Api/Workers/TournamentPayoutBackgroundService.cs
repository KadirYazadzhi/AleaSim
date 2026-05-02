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
        var tournament = scope.ServiceProvider.GetRequiredService<ITournamentService>();
        var realTime = scope.ServiceProvider.GetRequiredService<IRealTimeService>();

        // Trigger the unified logic
        // This method now handles dates, locking, and season rotation internally
        await tournament.ProcessMonthlyPayout(repo, vault, realTime);
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
