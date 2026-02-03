using AleaSim.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AleaSim.Domain.Entities;

public class User {
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = "https://api.dicebear.com/7.x/bottts/svg?seed=default";
    public decimal Balance { get; set; }
    
    // --- Bonus Wallet ---
    public decimal BonusBalance { get; set; } = 0m;
    public decimal WageringRequirement { get; set; } = 0m; // Target amount to bet
    public decimal WageringProgress { get; set; } = 0m;   // Amount already bet
    public DateTime? BonusLastUpdated { get; set; }
    
    // --- Activity Tracking ---
    public DateTime? LastBetTimestamp { get; set; }
    public DateTime? LastDailySpin { get; set; }
    
    // --- Streak Tracking ---
    public int CurrentStreak { get; set; } = 0;
    public DateTime? LastStreakClaim { get; set; }

    public Role Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LockoutUntil { get; set; } // For cool-down periods

    // Navigation Properties
    public virtual PlayerProfile? Profile { get; set; }
    public virtual ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
