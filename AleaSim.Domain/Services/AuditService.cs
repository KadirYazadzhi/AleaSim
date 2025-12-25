using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class AuditService : IAuditService {
    private readonly List<AuditEvent> _logs = new();
    private string _lastHash = "GENESIS";

    public void LogEvent(string eventType, string description, string userId, string metadataJson) {
        lock (_logs) {
            var auditEvent = new AuditEvent {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                Description = description,
                UserId = userId,
                MetadataJson = metadataJson,
                PreviousHash = _lastHash
            };

            auditEvent.Hash = CalculateHash(auditEvent);
            _lastHash = auditEvent.Hash;
            _logs.Add(auditEvent);
        }
    }

    public IEnumerable<AuditEvent> GetLogs() {
        lock (_logs) {
            return _logs.ToList();
        }
    }

    public bool VerifyIntegrity() {
        lock (_logs) {
            string expectedPreviousHash = "GENESIS";
            foreach (var log in _logs) {
                if (log.PreviousHash != expectedPreviousHash) return false;
                if (log.Hash != CalculateHash(log)) return false;
                expectedPreviousHash = log.Hash;
            }
        }
        return true;
    }

    private string CalculateHash(AuditEvent ev) {
        string data = $"{ev.Timestamp:O}|{ev.EventType}|{ev.UserId}|{ev.MetadataJson}|{ev.PreviousHash}";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(bytes);
    }
}
