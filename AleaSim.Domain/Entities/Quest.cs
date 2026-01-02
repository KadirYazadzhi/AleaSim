namespace AleaSim.Domain.Entities;

public enum QuestStatus {
    Active,
    Completed,
    Claimed,
    Expired
}

public class Quest {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty; // "SpinCount", "WinAmount", "SymbolCollect"
    public string Description { get; set; } = string.Empty; // "Spin 50 times on Clover Chase"
    
    public int TargetValue { get; set; }
    public int CurrentProgress { get; set; }
    
    public decimal RewardAmount { get; set; }
    
    public QuestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
