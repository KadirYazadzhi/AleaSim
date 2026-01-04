using System.Collections.Generic;

namespace AleaSim.Shared.Models;

public class UserProfileResponse {
    public string Username { get; set; } = "";
    public decimal Balance { get; set; }
    public decimal BonusBalance { get; set; }
    public string AvatarUrl { get; set; } = "";
    public string SymbolAffinityJson { get; set; } = "{}";
    public string? ActiveGameStateJson { get; set; } // Added for recovery
    public int LuckyCloverLevel { get; set; }
    public int CashbackLevel { get; set; }
    public int XpBoostLevel { get; set; }
    public int CurrentStreak { get; set; }
    public UserProgressionDto Progression { get; set; } = new();
    public List<UserAchievementDto> Achievements { get; set; } = new();
}
