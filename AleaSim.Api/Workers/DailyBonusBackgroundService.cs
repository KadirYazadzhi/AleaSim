using AleaSim.Domain.Interfaces;

namespace AleaSim.Api.Workers;

public class DailyBonusBackgroundService : BackgroundService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyBonusBackgroundService> _logger;

    public DailyBonusBackgroundService(IServiceScopeFactory scopeFactory, ILogger<DailyBonusBackgroundService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        // Wait until next 00:01 UTC
        var now = DateTime.UtcNow;
        var nextRun = now.Date.AddDays(1).AddMinutes(1);
        var delay = nextRun - now;

        if (delay.TotalMilliseconds <= 0) delay = TimeSpan.FromMinutes(1); // Safety

        _logger.LogInformation("Daily Bonus Job scheduled in {Delay}", delay);
        await Task.Delay(delay, stoppingToken);

        // Daily Loop
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await ProcessDailyTasks();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error in Daily Bonus Job");
            }

            // Wait 24 hours
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task ProcessDailyTasks() {
        _logger.LogInformation("Starting Daily Processing...");
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var vault = scope.ServiceProvider.GetRequiredService<IVaultService>();
        var realTime = scope.ServiceProvider.GetRequiredService<IRealTimeService>();

        // 1. Process Tournament (If yesterday was 30th)
        var yesterday = DateTime.UtcNow.AddDays(-1);
        if (yesterday.Day == 30) {
            _logger.LogInformation("Finalizing Tournament for {Date}", yesterday.Date);
            var winners = repo.GetTopTournamentEntries(yesterday, 10);
            
            decimal prizePool = 20000;
            int rank = 1;
            
            foreach (var winner in winners) {
                // Simple distribution: 1st=50%, 2nd=25%, etc... or just fixed
                decimal prize = prizePool / (rank * 2); // Decay
                if (rank == 10) prize = prizePool - (prizePool * 0.9m); // Remainder? Just Mock logic.
                
                // Real Logic:
                decimal[] payouts = { 5000, 3000, 2000, 1500, 1000, 800, 700, 600, 500, 400 }; // Total ~15k
                decimal actualPrize = rank <= 10 ? payouts[rank - 1] : 0;

                vault.CreditBonus(winner.UserId, actualPrize, actualPrize, repo);
                await realTime.NotifyGameUpdate(winner.UserId, new { Type = "TournamentWin", Rank = rank, Amount = actualPrize });
                
                rank++;
            }
        }

        // 2. Daily Bonuses (Cashback / Loyalty)
        // This requires iterating ALL users or having a "DailyStats" table.
        // For MVP, we skip iteration over 1M users. We assume we fetch "Active Yesterday" users.
        // Mock implementation:
        // var dailyStats = repo.GetDailyUserStats(yesterday); ...
        
        _logger.LogInformation("Daily Processing Complete.");
    }
}
