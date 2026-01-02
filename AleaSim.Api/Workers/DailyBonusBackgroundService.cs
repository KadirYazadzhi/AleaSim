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
        var dailyStats = repo.CalculateDailyNet(yesterday);
        
        foreach (var stat in dailyStats) {
            decimal bonusAmount = 0;
            string type = "";

            if (stat.NetResult < 0) {
                // LOSS: Cashback 10%
                decimal loss = Math.Abs(stat.NetResult);
                bonusAmount = loss * 0.10m;
                type = "Cashback";
            }
            else if (stat.NetResult > 0) {
                // WIN: Loyalty 5%
                bonusAmount = stat.NetResult * 0.05m;
                type = "Loyalty Reward";
            }

            if (bonusAmount > 0) {
                // Credit to Bonus Wallet (1x wagering required)
                // If bonus < 100, no option to cashout 1/10 (handled in Vault/UI later)
                vault.CreditBonus(stat.UserId, bonusAmount, bonusAmount, repo);
                
                await realTime.NotifyGameUpdate(stat.UserId, new { 
                    Type = "DailyBonus", 
                    BonusType = type, 
                    Amount = bonusAmount,
                    Message = $"You received a {type} of {bonusAmount:C}!"
                });
            }
        }
        // 3. Bonus Expiry (7 Days)
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var expiredUsers = repo.GetUsersWithExpiredBonuses(sevenDaysAgo);
        
        foreach (var user in expiredUsers) {
            _logger.LogInformation("Expiring bonus for User {UserId}: {Amount}", user.Id, user.BonusBalance);
            user.BonusBalance = 0;
            user.WageringRequirement = 0;
            user.WageringProgress = 0;
            user.BonusLastUpdated = null;
            repo.UpdateUser(user);
            
            await realTime.NotifyGameUpdate(user.Id, new { 
                Type = "BonusExpired", 
                Message = "Your unused bonus has expired."
            });
        }
        
        _logger.LogInformation("Daily Processing Complete.");
    }
}
