using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AleaSim.Api.Workers;

public class AuditWriterBackgroundService : BackgroundService {
    private readonly IAuditBuffer _buffer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditWriterBackgroundService> _logger;

    public AuditWriterBackgroundService(IAuditBuffer buffer, IServiceScopeFactory scopeFactory, ILogger<AuditWriterBackgroundService> logger) {
        _buffer = buffer;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Audit Writer Background Service initializing...");
        
        // Wait for DB migrations in Program.cs
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        
        _logger.LogInformation("Audit Writer Background Service started.");

        var batch = new List<AuditEvent>();
        var lastFlush = DateTime.UtcNow;

        try {
            // Read until the channel is closed, even if stoppingToken is triggered
            // Use a combined approach: Read with stoppingToken, then drain remaining on cancellation
            while (!stoppingToken.IsCancellationRequested) {
                await foreach (var ev in _buffer.DequeueAllAsync(stoppingToken)) {
                    batch.Add(ev);

                    if (batch.Count >= 100 || (DateTime.UtcNow - lastFlush).TotalSeconds >= 5) {
                        await FlushBatch(batch);
                        lastFlush = DateTime.UtcNow;
                    }
                }
            }
        }
        catch (OperationCanceledException) {
            _logger.LogInformation("Audit Writer received shutdown signal. Draining buffer...");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Critical error in Audit Writer");
        }
        finally {
            // DRAIN REMAINING ON SHUTDOWN
            // We use a new cancellation token with timeout to avoid hanging forever
            using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try {
                await foreach (var ev in _buffer.DequeueAllAsync(shutdownCts.Token)) {
                    batch.Add(ev);
                    if (batch.Count >= 100) {
                        await FlushBatch(batch);
                    }
                }
            } catch { /* Timeout or channel closed */ }
            
            if (batch.Any()) await FlushBatch(batch);
            _logger.LogInformation("Audit Writer shutdown complete.");
        }
    }

    private Task FlushBatch(List<AuditEvent> batch) {
        if (!batch.Any()) return Task.CompletedTask;

        try {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
            repo.LogAuditBatch(batch);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to flush audit batch of {Count} events. Logs are dropped to prevent memory exhaustion.", batch.Count);
            // Note: In production, consider dead-letter storage
        }
        finally {
            batch.Clear();
        }
        return Task.CompletedTask;
    }
}
