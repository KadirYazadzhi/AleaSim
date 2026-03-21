using System.Text.Json.Serialization;

namespace AleaSim.Shared.Models;

public class JackpotDto {
    public Guid Id { get; set; }
    public Guid? GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal CurrentValue { get; set; }
    public decimal? MustDropAt { get; set; }
    public bool IsGlobal { get; set; }
    public string Tier { get; set; } = string.Empty; // Clubs, Diamonds, Hearts, Spades
    
    [JsonIgnore]
    public double ProgressPercentage => MustDropAt.HasValue && MustDropAt.Value > 0 
        ? (double)(CurrentValue / MustDropAt.Value) * 100 
        : 0;
        
    [JsonIgnore]
    public bool IsHot => ProgressPercentage > 90;
}
