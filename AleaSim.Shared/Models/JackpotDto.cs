namespace AleaSim.Shared.Models;

public class JackpotDto {
    public string Name { get; set; } = string.Empty;
    public decimal CurrentValue { get; set; }
    public decimal? MustDropAt { get; set; }
    public bool IsGlobal { get; set; }
    public string Tier { get; set; } = string.Empty; // Clubs, Diamonds, Hearts, Spades
    
    public double ProgressPercentage => MustDropAt.HasValue && MustDropAt.Value > 0 
        ? (double)(CurrentValue / MustDropAt.Value) * 100 
        : 0;
        
    public bool IsHot => ProgressPercentage > 90;
}
