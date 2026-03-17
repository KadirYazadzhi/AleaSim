using System.Text.Json.Serialization;

namespace AleaSim.Shared.Models;

public class JackpotDto {
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("currentValue")]
    public decimal CurrentValue { get; set; }

    [JsonPropertyName("mustDropAt")]
    public decimal? MustDropAt { get; set; }

    [JsonPropertyName("isGlobal")]
    public bool IsGlobal { get; set; }

    [JsonPropertyName("tier")]
    public string Tier { get; set; } = string.Empty; // Clubs, Diamonds, Hearts, Spades
    
    [JsonIgnore]
    public double ProgressPercentage => MustDropAt.HasValue && MustDropAt.Value > 0 
        ? (double)(CurrentValue / MustDropAt.Value) * 100 
        : 0;
        
    [JsonIgnore]
    public bool IsHot => ProgressPercentage > 90;
}
