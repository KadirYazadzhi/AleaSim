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
        // For development/demo purposes, run 30 seconds after startup, then every 24 hours.
        // In PROD, this should be scheduled for 00:01 UTC.
        
        _logger.LogInformation("Daily Bonus Job waiting for system warm-up...");
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        // Daily Loop
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await ProcessDailyTasks();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error in Daily Bonus Job");
            }

            // Wait 24 hours (or 5 minutes for demo)
            // await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
             _logger.LogInformation("Daily Bonus Job sleeping for 24 hours...");
             await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task ProcessDailyTasks() {
        _logger.LogInformation("Starting Daily Processing...");
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var vault = scope.ServiceProvider.GetRequiredService<IVaultService>();
        var realTime = scope.ServiceProvider.GetRequiredService<IRealTimeService>();
        var tournament = scope.ServiceProvider.GetRequiredService<ITournamentService>();

        // Wrap everything in a transaction because VaultService no longer commits internally
        using var tx = repo.BeginTransaction();
        try {
            // 1. Process Tournament (If first day of new month)
            var today = DateTime.UtcNow;
            if (today.Day == 1) {
                await tournament.ProcessMonthlyPayout(repo, vault, realTime);
            }

            // 2. Daily Bonuses (Cashback / Loyalty)
            var yesterday = DateTime.UtcNow.AddDays(-1);
            var dailyStats = repo.CalculateDailyNet(yesterday);
            
            foreach (var stat in dailyStats) {
                decimal bonusAmount = 0;
                string type = "";

                if (stat.NetResult > 0) {
                    // Loyalty reward for winners (5% of daily profit)
                    bonusAmount = stat.NetResult * 0.05m;
                    type = "Loyalty Reward";
                }

                if (bonusAmount > 0.01m) {
                    await vault.CreditBonusAsync(stat.UserId, bonusAmount, bonusAmount, repo); 
                    
                    _logger.LogInformation("Awarded {Type}: {Amount} to User {UserId}", type, bonusAmount, stat.UserId);

                    _ = realTime.NotifyGameUpdate(stat.UserId, new { 
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

            // Commit all changes (Tournament payouts, Daily Bonuses, Expirations)
            repo.SaveChanges();
            tx.Commit();
            _logger.LogInformation("Daily Processing Complete & Committed.");
        }
        catch (Exception ex) {
            tx.Rollback();
            _logger.LogError(ex, "Transaction Failed during Daily Processing");
        }
    }
}
