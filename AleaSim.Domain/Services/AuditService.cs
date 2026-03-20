using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;

namespace AleaSim.Domain.Services;

public class AuditService : IAuditService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuditBuffer _buffer;
    private readonly IRealTimeService _realTime;
    private string _lastHash = "GENESIS";

    public AuditService(IServiceScopeFactory scopeFactory, IAuditBuffer buffer, IRealTimeService realTime) {
        _scopeFactory = scopeFactory;
        _buffer = buffer;
        _realTime = realTime;
        InitializeLastHash();
    }

    private void InitializeLastHash() {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var lastHash = repo.GetLastAuditHash();
        if (lastHash != null) {
            _lastHash = lastHash;
        }
    }

    public void LogEvent(string eventType, string description, string userId, string metadataJson) {
        lock (this) {
            var now = DateTime.UtcNow;
            // Truncate to seconds to ensure perfect consistency with all DB providers (MySQL/SQLite/Postgres)
            var safeTimestamp = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc);

            var auditEvent = new AuditEvent {
                Id = Guid.NewGuid(),
                Timestamp = safeTimestamp,
                EventType = eventType,
                Description = description,
                UserId = userId,
                MetadataJson = metadataJson ?? "{}",
                PreviousHash = _lastHash
            };

            var hash = CalculateHash(auditEvent);
            auditEvent.Hash = hash;

            // Buffer the event for background batch writing
            _buffer.Enqueue(auditEvent);
            
            // Notify Admins in Real-Time
            _ = _realTime.NotifyAuditLog(auditEvent);

            // Update memory immediately to maintain hash chain continuity
            _lastHash = hash;
        }
    }

    public IEnumerable<AuditEvent> GetLogs() {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        return repo.GetAuditLogs(100);
    }

    public bool VerifyIntegrity() {
        bool isValid = false;
        try {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
            
            // IMPORTANT: Must sort by Timestamp AND Id to ensure deterministic order 
            // when multiple records share the same second-precision timestamp.
            var logs = repo.GetAllAuditLogs()
                .OrderBy(x => x.Timestamp)
                .ThenBy(x => x.Id)
                .ToList();
            
            if (!logs.Any()) {
                isValid = true;
            } else {
                string expectedPreviousHash = "GENESIS";
                isValid = true;

                foreach (var log in logs) {
                    // Skip logging our own verify event if it was just added mid-loop (unlikely due to sync)
                    if (log.EventType == "SYSTEM_INTEGRITY_CHECK") continue;

                    // 1. Check Link: Does PreviousHash match?
                    if (log.PreviousHash != expectedPreviousHash) {
                        isValid = false; // Broken chain link
                        break;
                    }

                    // 2. Check Data: Does Hash match Content?
                    string calculatedHash = CalculateHash(log);
                    if (log.Hash != calculatedHash) {
                        isValid = false; // Data tampering
                        break;
                    }

                    // Update for next iteration
                    expectedPreviousHash = log.Hash;
                }
            }
        }
        finally {
            LogEvent("SYSTEM_INTEGRITY_CHECK", $"Manual integrity scan performed. Result: {(isValid ? "PASS" : "FAIL")}", "SYSTEM", $"{{\"IsValid\": {isValid.ToString().ToLower()}}}");
        }

        return isValid; 
    }

    public async Task RepairIntegrity() {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        
        // Ensure deterministic order for identical timestamps
        var allLogs = repo.GetAllAuditLogs()
            .OrderBy(x => x.Timestamp)
            .ThenBy(x => x.Id)
            .ToList();
        
        if (!allLogs.Any()) return;

        string currentHash = "GENESIS";
        
        foreach (var log in allLogs) {
            // Force fix timestamp to seconds if they have milliseconds (cleaning up old records)
            var dt = log.Timestamp;
            log.Timestamp = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, DateTimeKind.Utc);
            
            log.PreviousHash = currentHash;
            log.Hash = CalculateHash(log);
            currentHash = log.Hash;
        }
        
        repo.SaveChanges();
        
        lock(this) {
            _lastHash = currentHash;
        }
        
        LogEvent("SYSTEM_INTEGRITY_REPAIR", "Full hash chain re-calculation performed.", "SYSTEM", "{}");
        await Task.CompletedTask;
    }

    private string CalculateHash(AuditEvent ev) {
        // Use a fixed format string for hashing to avoid DateTime.ToString() variations
        string ts = ev.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        string metadata = ev.MetadataJson ?? "{}";
        string data = $"{ts}|{ev.EventType}|{ev.UserId}|{metadata}|{ev.PreviousHash}";
        
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(bytes);
    }
}
