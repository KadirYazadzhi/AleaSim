using AleaSim.Domain.Interfaces;

namespace AleaSim.Api.Workers;

public class RaffleBackgroundService : BackgroundService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RaffleBackgroundService> _logger;
    private readonly IRngService _rngService; // Injected singleton

    public RaffleBackgroundService(IServiceScopeFactory scopeFactory, ILogger<RaffleBackgroundService> logger, IRngService rngService) {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _rngService = rngService;
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
        bool isMonthly = now.Day == 30; // Still simplistic, but acceptable for MVP

        if (!isWeekly && !isMonthly) return;

        // Chance to drop a prize ~ 16% per minute
        if (_rngService.GetNextDouble((int)now.Ticks, 777) < 0.16) {
            using var scope = _scopeFactory.CreateScope();
            var promoService = scope.ServiceProvider.GetRequiredService<IPromotionService>();
            var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();

            using var tx = repo.BeginTransaction();
            try {
                if (isWeekly) {
                    decimal prize = _rngService.GetNextInt((int)now.Ticks, 1, 1, 10) * 500m; 
                    _logger.LogInformation("Triggering Weekly Raffle Drop: {Amount}", prize);
                    await promoService.ExecuteRaffleDraw(prize, "Weekly", repo);
                }
                else if (isMonthly) {
                    decimal prize = _rngService.GetNextInt((int)now.Ticks, 2, 2, 20) * 500m;
                    _logger.LogInformation("Triggering Monthly Raffle Drop: {Amount}", prize);
                    await promoService.ExecuteRaffleDraw(prize, "Monthly", repo);
                }
                tx.Commit();
            } catch (Exception ex) {
                tx.Rollback();
                _logger.LogError(ex, "Raffle Transaction Failed");
            }
        }
    }
}
