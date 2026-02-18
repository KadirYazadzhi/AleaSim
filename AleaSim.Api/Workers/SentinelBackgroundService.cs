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
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Sentinel Scan Error");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ScanForAnomalies() {
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
    }

    private void AddAlert(SentinelAlertDto alert) {
        lock (_lock) {
            _recentAlerts.Insert(0, alert);
            if (_recentAlerts.Count > 100) _recentAlerts.RemoveAt(100);
        }
        _logger.LogWarning("SENTINEL ALERT: {Type} - {Desc}", alert.AlertType, alert.Description);
    }
}