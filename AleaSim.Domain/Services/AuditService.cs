using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;

namespace AleaSim.Domain.Services;

public class AuditService : IAuditService {
    private readonly IServiceScopeFactory _scopeFactory;
    private string _lastHash = "GENESIS";

    public AuditService(IServiceScopeFactory scopeFactory) {
        _scopeFactory = scopeFactory;
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
            var auditEvent = new AuditEvent {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                Description = description,
                UserId = userId,
                MetadataJson = metadataJson,
                PreviousHash = _lastHash
            };

            var hash = CalculateHash(auditEvent);
            auditEvent.Hash = hash;

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
            repo.LogAudit(auditEvent);
            
            // Only update memory after successful persistence
            _lastHash = hash;
        }
    }

    public IEnumerable<AuditEvent> GetLogs() {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        return repo.GetAuditLogs(100);
    }

    public bool VerifyIntegrity() {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        
        var logs = repo.GetAllAuditLogs().ToList();
        if (!logs.Any()) return true;

        string expectedPreviousHash = "GENESIS";

        foreach (var log in logs) {
            // 1. Check Link: Does PreviousHash match?
            if (log.PreviousHash != expectedPreviousHash) {
                return false; // Broken chain link
            }

            // 2. Check Data: Does Hash match Content?
            string calculatedHash = CalculateHash(log);
            if (log.Hash != calculatedHash) {
                return false; // Data tampering
            }

            // Update for next iteration
            expectedPreviousHash = log.Hash;
        }

        return true; 
    }

    private string CalculateHash(AuditEvent ev) {
        string data = $"{ev.Timestamp:O}|{ev.EventType}|{ev.UserId}|{ev.MetadataJson}|{ev.PreviousHash}";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(bytes);
    }
}
