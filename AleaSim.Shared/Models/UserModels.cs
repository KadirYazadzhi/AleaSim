using System.Collections.Generic;

namespace AleaSim.Shared.Models;

public class UserProfileResponse {
    public string Username { get; set; } = "";
    public decimal Balance { get; set; }
    public decimal BonusBalance { get; set; }
    public decimal TotalBalance => Balance + BonusBalance;
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

public class UserDto {
    public string Username { get; set; } = "";
    public decimal Balance { get; set; }
    public decimal BonusBalance { get; set; }
    public decimal TotalBalance => Balance + BonusBalance;
    public string AvatarUrl { get; set; } = "";
    public string ActiveGameStateJson { get; set; } = "";
    public string Role { get; set; } = "";
    public int LuckyCloverLevel { get; set; }
    public int CashbackLevel { get; set; }
    public int XpBoostLevel { get; set; }
    public int CurrentStreak { get; set; } // Added
    public UserProgressionDto Progression { get; set; } = new();
    public List<UserAchievementDto> Achievements { get; set; } = new();
}

public class BalanceUpdateDto {
    public decimal Balance { get; set; }
    public decimal BonusBalance { get; set; }
}

