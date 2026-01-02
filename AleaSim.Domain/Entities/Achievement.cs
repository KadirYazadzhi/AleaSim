namespace AleaSim.Domain.Entities;

public class Achievement {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty; // MudBlazor Icon name or Emoji
    public string Category { get; set; } = "General"; // Slots, Roulette, Blackjack, Social
    public decimal RewardAmount { get; set; } // Cash reward for unlocking
    
    // Condition logic (simplified for prototype)
    public string ConditionType { get; set; } = string.Empty; // "TotalSpins", "BigWinCount", "LevelReached"
    public decimal ConditionValue { get; set; }
}

public class UserAchievement {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AchievementId { get; set; }
    public DateTime UnlockedAt { get; set; }
    
    public virtual Achievement Achievement { get; set; } = null!;
}
