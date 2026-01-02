namespace AleaSim.Shared.Models;

public class UserProgressionDto {
    public int CurrentLevel { get; set; }
    public decimal CurrentXP { get; set; }
    public int SkillPoints { get; set; }
    public decimal LifetimeXP { get; set; }
    public decimal NextLevelXP => CurrentLevel * 1000;
    public double ProgressPercentage => (double)(CurrentXP / NextLevelXP) * 100;
}
