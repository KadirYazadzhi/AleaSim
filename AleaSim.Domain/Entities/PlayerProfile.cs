namespace AleaSim.Domain.Entities;

public class PlayerProfile {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // --- Behavioral Metrics ---
    public int VolatilityScore { get; set; } = 5; // 1 (Low) to 10 (High). Default 5.
    public double ChurnRiskScore { get; set; } = 0.0; // 0.0 to 1.0 (Probability of leaving)
    
    // --- Financial Metrics ---
    public decimal TotalWagered { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal NetDeposit { get; set; } // LTV (Lifetime Value)

    // --- Promotion Tracking ---
    public decimal WeeklyWagered { get; set; }
    public decimal MonthlyWagered { get; set; }

    // --- RTP Tracking ---
    public double TheoreticalRtp { get; set; } = 0.95; // Target
    public double ActualRtp { get; set; } = 0.0;       // Current reality

    // --- Session State ---
    public decimal CurrentSessionRtp { get; set; }
    public int LossStreak { get; set; }
    
    // --- Flow State ---
    public double AvgSpinInterval { get; set; } = 5.0; // Seconds
    public DateTime LastSpinTimestamp { get; set; } = DateTime.UtcNow;

    // --- Persona & Affinity ---
    public string SymbolAffinityJson { get; set; } = "{}"; // Map of SymbolID -> Score

    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
}
