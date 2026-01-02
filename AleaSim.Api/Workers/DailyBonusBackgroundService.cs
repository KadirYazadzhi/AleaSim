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

        // 1. Process Tournament (Mock logic for now)
        var yesterday = DateTime.UtcNow.AddDays(-1);
        
        // 2. Daily Bonuses (Cashback / Loyalty)
        // Using CalculateDailyNet which returns (UserId, NetResult) tuple
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

            if (bonusAmount > 0.01m) { // Ignore tiny amounts
                vault.CreditBonus(stat.UserId, bonusAmount, bonusAmount, repo); // 1x wagering
                
                _logger.LogInformation("Awarded {Type}: {Amount} to User {UserId}", type, bonusAmount, stat.UserId);

                // Notify if online
                // Note: RealTimeService might need error handling if user is offline, usually handled internally
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
        
        _logger.LogInformation("Daily Processing Complete.");
    }
}
