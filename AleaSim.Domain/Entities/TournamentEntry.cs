namespace AleaSim.Domain.Entities;

public class TournamentEntry {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public DateTime TournamentDate { get; set; } // The date (e.g., 2023-10-30)
    
    public decimal TotalWagered { get; set; }
    public decimal TotalPayout { get; set; }
    public int RoundCount { get; set; }
    
    // Calculated ROI: ((TotalPayout - TotalWagered) / TotalWagered) * 100
    public decimal RoiPercentage { 
        get {
            if (TotalWagered == 0) return 0;
            return ((TotalPayout - TotalWagered) / TotalWagered) * 100;
        }
    }
}
