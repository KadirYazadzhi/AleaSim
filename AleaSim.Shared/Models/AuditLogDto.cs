namespace AleaSim.Shared.Models;

public class AuditLogDto {
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = string.Empty;
}
