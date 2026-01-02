namespace AleaSim.Shared.Models;

public enum VipTier {
    Bronze,   // Lv 1
    Silver,   // Lv 10
    Gold,     // Lv 25
    Platinum, // Lv 50
    Diamond   // Lv 80
}

public class UserProgressionDto {
    public int CurrentLevel { get; set; }
    public decimal CurrentXP { get; set; }
    public int SkillPoints { get; set; }
    public decimal LifetimeXP { get; set; }
    public decimal NextLevelXP => CurrentLevel * 1000;
    public double ProgressPercentage => (double)(CurrentXP / NextLevelXP) * 100;

    public VipTier Tier => CurrentLevel switch {
        >= 80 => VipTier.Diamond,
        >= 50 => VipTier.Platinum,
        >= 25 => VipTier.Gold,
        >= 10 => VipTier.Silver,
        _ => VipTier.Bronze
    };

    public string TierName => Tier.ToString().ToUpper();
}
