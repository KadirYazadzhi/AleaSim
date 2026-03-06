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

    // Stats
    public decimal TotalWagered { get; set; }
    public decimal TotalWon { get; set; }
    public int TotalRounds { get; set; }
    public double? WinLossRatio => TotalWagered > 0 ? (double)(TotalWon / TotalWagered) : 0;
    public string FavoriteGame { get; set; } = "N/A";
    public List<double> RecentWinLossTrend { get; set; } = new();

    // Settings
    public bool IsTwoFactorEnabled { get; set; }
    public decimal? DailyLossLimit { get; set; }
    public decimal? WeeklyLossLimit { get; set; }
    public decimal? MonthlyLossLimit { get; set; }
    public string PreferencesJson { get; set; } = "{}";
    public DateTime? LockoutUntil { get; set; }
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
    
    // Settings
    public bool IsTwoFactorEnabled { get; set; }
    public decimal? DailyLossLimit { get; set; }
    public decimal? WeeklyLossLimit { get; set; }
    public decimal? MonthlyLossLimit { get; set; }
    public string PreferencesJson { get; set; } = "{}";
}

public class BalanceUpdateDto {
    public decimal Balance { get; set; }
    public decimal BonusBalance { get; set; }
}

