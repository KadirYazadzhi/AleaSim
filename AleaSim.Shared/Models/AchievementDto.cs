namespace AleaSim.Shared.Models;

public class UserAchievementDto {
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public DateTime UnlockedAt { get; set; }
}
