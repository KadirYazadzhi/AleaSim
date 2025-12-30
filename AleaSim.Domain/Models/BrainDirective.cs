namespace AleaSim.Domain.Models;

public class BrainDirective {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DecisionType { get; set; } = "Random"; // Random, RetentionHook, WhaleBonus, CoolDown
    public decimal TargetWinAmount { get; set; } // The exact amount user should win
    public bool IsNearMiss { get; set; } // If true, generate a "Teaser" loss
    public double AllowedDeviation { get; set; } = 0.0; // Allowed variance (e.g. +/- 10%)
    public string Reason { get; set; } = string.Empty; // Logging reason
}
