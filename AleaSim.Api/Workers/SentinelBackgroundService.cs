using AleaSim.Domain.Interfaces;
using AleaSim.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AleaSim.Api.Workers;

public class SentinelBackgroundService : BackgroundService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SentinelBackgroundService> _logger;
    private readonly List<SentinelAlertDto> _recentAlerts = new(); // In-memory for current session
    private readonly object _lock = new();

    public SentinelBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SentinelBackgroundService> logger) {
        _scopeFactory = scopeFactory; 
        _logger = logger;
    }

    public IEnumerable<SentinelAlertDto> GetAlerts() {
        lock (_lock) {
            return _recentAlerts.ToList();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Sentinel Security Monitor started.");

        while (!stoppingToken.IsCancellationRequested) {
            try {
                await ScanForAnomalies();
                await ReconcileBalances();
                await PeriodicCleanup();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Sentinel Scan Error");
            }

            // Runs every 5 minutes for balance reconciliation, 30s for general scan
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task PeriodicCleanup() {
        using var scope = _scopeFactory.CreateScope();
        var redis = scope.ServiceProvider.GetRequiredService<AleaSim.Domain.Services.IRedisCacheService>();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();

        string lockKey = "sentinel:last_cleanup";
        var lastRun = await redis.GetAsync<DateTime?>(lockKey);
        
        // Run once a day
        if (lastRun != null && (DateTime.UtcNow - lastRun.Value).TotalHours < 24) return;
        await redis.SetAsync(lockKey, DateTime.UtcNow, TimeSpan.FromHours(25));

        _logger.LogInformation("Sentinel: Performing Periodic System Cleanup...");
        repo.CleanupOldRtpStats(30); // Keep last 30 days of stats
        repo.CleanupOldAuditLogs(90); // Keep last 90 days of audit logs
        _logger.LogInformation("Sentinel: System Cleanup complete.");
    }

    private async Task ReconcileBalances() {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        
        // Use Redis to only run this once every 10 minutes system-wide
        var redis = scope.ServiceProvider.GetRequiredService<AleaSim.Domain.Services.IRedisCacheService>();
        string lockKey = "sentinel:last_reconciliation";
        var lastRun = await redis.GetAsync<DateTime?>(lockKey);
        
        if (lastRun != null && (DateTime.UtcNow - lastRun.Value).TotalMinutes < 10) return;
        await redis.SetAsync(lockKey, DateTime.UtcNow, TimeSpan.FromMinutes(15));

        _logger.LogInformation("Sentinel: Starting Financial Reconciliation...");
        
        var users = repo.GetAllUsers();
        foreach (var user in users) {
            if (user.Username.StartsWith("Sim_")) continue; // Skip simulation users

            var transactions = repo.GetUserTransactions(user.Id, 1000); // Check last 1000 tx
            decimal calculatedBalance = transactions.Sum(t => t.Amount); 
            
            // Note: This logic assumes user starts at 0 and all credit/debit are in Transactions.
            // For AleaSim, starting balance is 5000 or 1000000 for Admin.
            decimal expectedStartingBalance = (user.Role == AleaSim.Domain.Enums.Role.Admin) ? 1000000m : 5000m;
            decimal diff = Math.Abs(user.Balance - (expectedStartingBalance + calculatedBalance));

            if (diff > 0.01m) {
                AddAlert(new SentinelAlertDto {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Username = user.Username,
                    AlertType = "BalanceMismatch",
                    Severity = "Critical",
                    Description = $"Balance discrepancy: DB={user.Balance:C}, TX_SUM={expectedStartingBalance + calculatedBalance:C}. Diff={diff:C}",
                    Timestamp = DateTime.UtcNow
                });
                
                audit.LogEvent("SECURITY_ALERT", $"Financial Discrepancy for {user.Username}. Diff: {diff}", user.Id.ToString(), $"{{ \"Expected\": {expectedStartingBalance + calculatedBalance}, \"Actual\": {user.Balance} }}");
            }
        }
        _logger.LogInformation("Sentinel: Financial Reconciliation complete.");
    }

    private Task ScanForAnomalies() {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        
        // 1. Scan for High Payouts (> 500x)
        var recentRounds = repo.GetGlobalRecentRounds(100);
        
        foreach (var round in recentRounds) {
            if (round.TotalBetAmount > 0 && (round.TotalWinAmount / round.TotalBetAmount) >= 500) {
                AddAlert(new SentinelAlertDto {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Empty, // Would need user from session
                    Username = "System-wide Check",
                    AlertType = "HighPayout",
                    Severity = "High",
                    Description = $"Abnormal win detected: {round.TotalWinAmount / round.TotalBetAmount:F0}x multiplier.",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        // 2. Bot Detection (Rapid betting)
        // This would require a group-by logic on recent bets in repo.
        // For MVP, we log the scan action.
        _logger.LogDebug("Sentinel scan complete. No critical threats found.");
        return Task.CompletedTask;
    }

    private void AddAlert(SentinelAlertDto alert) {
        lock (_lock) {
            _recentAlerts.Insert(0, alert);
            if (_recentAlerts.Count > 100) _recentAlerts.RemoveAt(100);
        }
        _logger.LogWarning("SENTINEL ALERT: {Type} - {Desc}", alert.AlertType, alert.Description);
    }
}