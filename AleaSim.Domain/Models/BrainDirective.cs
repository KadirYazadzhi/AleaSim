namespace AleaSim.Domain.Models;

public class BrainDirective {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DecisionType { get; set; } = "Random"; // Random, RetentionHook, WhaleBonus, CoolDown
    public decimal TargetWinAmount { get; set; } // The exact amount user should win
    public bool IsNearMiss { get; set; }
    public int? PreferredNearMissSymbol { get; set; } // The symbol player "chases"
    public string Reason { get; set; } = string.Empty;
}
