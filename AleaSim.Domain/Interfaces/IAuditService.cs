using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IAuditService {
    void LogEvent(string eventType, string description, string userId, string metadataJson);
    IEnumerable<AuditEvent> GetLogs();
    bool VerifyIntegrity();
    Task RepairIntegrity();
}
