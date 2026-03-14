using System;
using System.Collections.Generic;

namespace AleaSim.Shared.Models;

public class UserProfileResponse : UserDto {
    // This class is now redundant but kept for backward compatibility
    // and can be used for extra stats if needed.
}

public class UserDto {
    public string Username { get; set; } = "";
    public decimal Balance { get; set; }
    public decimal BonusBalance { get; set; }
    public decimal TotalBalance => Balance + BonusBalance;
    public string AvatarUrl { get; set; } = "";
    public string? ActiveGameStateJson { get; set; }
    public string Role { get; set; } = "";
    public string SymbolAffinityJson { get; set; } = "{}";
    
    // Stats & RPG
    public int LuckyCloverLevel { get; set; }
    public int CashbackLevel { get; set; }
    public int XpBoostLevel { get; set; }
    public int CurrentStreak { get; set; }
    public int FruitBlastLifetimeExplosions { get; set; }
    public UserProgressionDto Progression { get; set; } = new();
    public List<UserAchievementDto> Achievements { get; set; } = new();
    
    // Detailed Stats
    public decimal TotalWagered { get; set; }
    public decimal TotalWon { get; set; }
    public int TotalRounds { get; set; }
    public string FavoriteGame { get; set; } = "N/A";
    public List<double> RecentWinLossTrend { get; set; } = new();
    public int VolatilityScore { get; set; }
    public double ChurnRiskScore { get; set; }
    public decimal BiggestWin { get; set; }
    public decimal PendingCashback { get; set; }
    public double AvgSpinInterval { get; set; }
    public int LossStreak { get; set; }
    public decimal LuckFactor { get; set; }

    // Settings
    public bool IsTwoFactorEnabled { get; set; }
    public decimal? DailyLossLimit { get; set; }
    public decimal? WeeklyLossLimit { get; set; }
    public decimal? MonthlyLossLimit { get; set; }
    public string PreferencesJson { get; set; } = "{}";
    public DateTime? LockoutUntil { get; set; }
}

public class BalanceUpdateDto {
    public decimal Balance { get; set; }
    public decimal BonusBalance { get; set; }
}
