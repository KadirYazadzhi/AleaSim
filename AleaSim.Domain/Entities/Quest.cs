using System;

namespace AleaSim.Domain.Entities;

public class Quest {
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GoalType { get; set; } = "SpinCount"; // SpinCount, WinAmount, TotalWager
    public decimal TargetValue { get; set; }
    public decimal RewardAmount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UserQuestProgress {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid QuestId { get; set; }
    public decimal CurrentValue { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }

    public virtual Quest Quest { get; set; } = null!;
}
