using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Models;

public class AdminDashboardStats {
    public decimal Ggr { get; set; } // Gross Gaming Revenue (Bets - Wins)
    public decimal TotalBets { get; set; }
    public decimal TotalWins { get; set; }
    public decimal CurrentRtp { get; set; }
    public int ActivePlayerCount { get; set; }
    public bool IsEmergencyStopActive { get; set; }
    public List<string> TopWinners { get; set; } = new();
}

public class PlayerDossier {
    public User User { get; set; } = new();
    public PlayerProfile Profile { get; set; } = new();
    public decimal ActualRtp { get; set; }
    public decimal LifetimeValue { get; set; }
    public List<AuditEvent> RecentActivity { get; set; } = new();
}
