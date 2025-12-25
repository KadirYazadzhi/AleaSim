namespace AleaSim.Domain.Entities;

public class Jackpot {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal CurrentValue { get; set; }
    public decimal ContributionRate { get; set; } // Percentage of bet that goes to jackpot
    public bool IsGlobal { get; set; }
    public Guid? GameId { get; set; } // Null if global
    public DateTime LastUpdated { get; set; }
}
