namespace AleaSim.Domain.Entities;

public class AuditEvent {
    public long Id { get; set; } // AUTO-INCREMENT
    public int Sequence { get; set; } 
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty; // For chain validation
    public string PreviousHash { get; set; } = string.Empty;
}
