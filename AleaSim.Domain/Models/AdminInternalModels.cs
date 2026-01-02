using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Models;

public class PlayerDossier {
    public User User { get; set; } = new();
    public PlayerProfile Profile { get; set; } = new();
    public decimal ActualRtp { get; set; }
    public decimal LifetimeValue { get; set; }
    public List<AuditEvent> RecentActivity { get; set; } = new();
}
