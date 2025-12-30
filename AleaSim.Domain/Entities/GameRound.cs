namespace AleaSim.Domain.Entities;

public class GameRound {
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public int RoundNumber { get; set; }
    public string InputData { get; set; } = string.Empty;
    public string RandomResult { get; set; } = string.Empty;
    public decimal TotalBetAmount { get; set; }
    public decimal TotalWinAmount { get; set; }
    
    // --- Brain Context ---
    public string DecisionType { get; set; } = "Random"; // e.g., "RetentionHook", "WhaleBonus", "Random"
    public decimal TargetWinAmount { get; set; } // What the Brain requested
    public Guid? DirectiveId { get; set; }       // Link to Brain log (if any)

    public DateTime ExecutedAt { get; set; }
}
