namespace AleaSim.Domain.Entities;

public enum JackpotTier { Clubs, Diamonds, Hearts, Spades }

public class Jackpot {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public JackpotTier Tier { get; set; } = JackpotTier.Clubs; // Added
    public decimal CurrentValue { get; set; }
    public decimal ContributionRate { get; set; } // Percentage of bet that goes to jackpot
    public bool IsGlobal { get; set; }
    public Guid? GameId { get; set; } // Null if global
    
    // Must Drop Logic
    public decimal? MustDropAt { get; set; } // The cap. If null, it's a standard progressive.
    
    public DateTime LastUpdated { get; set; }
}
