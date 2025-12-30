using AleaSim.Domain.Interfaces;

namespace AleaSim.Api.Workers;

public class RaffleBackgroundService : BackgroundService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RaffleBackgroundService> _logger;

    public RaffleBackgroundService(IServiceScopeFactory scopeFactory, ILogger<RaffleBackgroundService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        // Run loop every minute
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            try {
                await CheckAndRunRaffle();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error in Raffle Scheduler");
            }
        }
    }

    private async Task CheckAndRunRaffle() {
        var now = DateTime.UtcNow;
        
        // Window: 19:00 - 21:00 UTC
        if (now.Hour < 19 || now.Hour >= 21) return;

        bool isWeekly = now.DayOfWeek == DayOfWeek.Sunday;
        bool isMonthly = now.Day == 30;

        if (!isWeekly && !isMonthly) return;

        // Chance to drop a prize in this minute (e.g., 10% chance per minute to spread drops)
        // Or strictly scheduled? Let's use random probability to spread 20 prizes over 120 mins.
        // 20 prizes / 120 mins = 1 prize every 6 mins approx.
        // Probability ~ 1/6 = 16%
        
        if (new Random().NextDouble() < 0.16) {
            using var scope = _scopeFactory.CreateScope();
            var promoService = scope.ServiceProvider.GetRequiredService<IPromotionService>();
            var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();

            if (isWeekly) {
                // Drop a random amount (500 - 5000)
                decimal prize = new Random().Next(1, 10) * 500m; 
                _logger.LogInformation("Triggering Weekly Raffle Drop: {Amount}", prize);
                await promoService.ExecuteRaffleDraw(prize, "Weekly", repo);
            }
            else if (isMonthly) {
                // Drop larger amount
                decimal prize = new Random().Next(2, 20) * 500m;
                _logger.LogInformation("Triggering Monthly Raffle Drop: {Amount}", prize);
                await promoService.ExecuteRaffleDraw(prize, "Monthly", repo);
            }
        }
    }
}
