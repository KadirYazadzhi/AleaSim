namespace AleaSim.Domain.Models;

public class BrainDirective {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DecisionType { get; set; } = "Random"; // Random, RetentionHook, WhaleBonus, CoolDown
    public decimal TargetWinAmount { get; set; } 
    public bool IsNearMiss { get; set; }
    public double VolatilityModifier { get; set; } = 1.0; // 1.0 = Normal, 2.0 = High, 0.5 = Low
    public int? PreferredNearMissSymbol { get; set; }
    public string Reason { get; set; } = string.Empty;
}
