using AleaSim.Domain.Entities;
using AleaSim.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AleaSim.Persistence;

public class AleaSimDbContext : DbContext {
    public DbSet<User> Users { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }
    public DbSet<PlayerProfile> PlayerProfiles { get; set; }
    public DbSet<TournamentEntry> TournamentEntries { get; set; }
    public DbSet<Game> Games { get; set; }
    public DbSet<GameSession> GameSessions { get; set; }
    public DbSet<Bet> Bets { get; set; }
    public DbSet<GameRound> GameRounds { get; set; }
    public DbSet<Outcome> Outcomes { get; set; }
    public DbSet<Jackpot> Jackpots { get; set; }
    public DbSet<RTPStatistics> RTPStatistics { get; set; }
    public DbSet<AuditEvent> AuditLogs { get; set; }
    public DbSet<GlobalSetting> GlobalSettings { get; set; }
    public DbSet<Quest> Quests { get; set; }
    public DbSet<UserProgression> UserProgressions { get; set; }
    public DbSet<Achievement> Achievements { get; set; }
    public DbSet<UserAchievement> UserAchievements { get; set; }
    public DbSet<Voucher> Vouchers { get; set; }
    public DbSet<UserVoucher> UserVouchers { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<TournamentWinner> TournamentWinners { get; set; }

    public AleaSimDbContext(DbContextOptions<AleaSimDbContext> options) : base(options) {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        // ... (existing configurations)
        modelBuilder.ApplyConfiguration(new Configurations.UserConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.GameConfiguration());
        // modelBuilder.ApplyConfiguration(new Configurations.GameSessionConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.GameRoundConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.BetConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.OutcomeConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.RTPStatisticsConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.PlayerProfileConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.JackpotConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.AuditEventConfiguration());

        // Enforce Cascade Delete for cleanup
        modelBuilder.Entity<User>().HasMany(u => u.GameSessions).WithOne().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<User>().HasMany(u => u.Transactions).WithOne().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<User>().HasOne(u => u.Profile).WithOne(p => p.User).HasForeignKey<PlayerProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GlobalSetting>(entity => {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(100);
            entity.Property(e => e.Value).HasMaxLength(500);
        });

        // Seed Achievements
        modelBuilder.Entity<Achievement>().HasData(
            new Achievement { Id = Guid.NewGuid(), Name = "First Blood", Description = "Place your first bet", Icon = "🎯", ConditionType = "TotalBets", ConditionValue = 1 },
            new Achievement { Id = Guid.NewGuid(), Name = "High Roller", Description = "Wager more than $5,000 total", Icon = "💎", ConditionType = "TotalWagered", ConditionValue = 5000 },
            new Achievement { Id = Guid.NewGuid(), Name = "The Whale", Description = "Wager more than $50,000 total", Icon = "🐋", ConditionType = "TotalWagered", ConditionValue = 50000 },
            new Achievement { Id = Guid.NewGuid(), Name = "Lucky Star", Description = "Hit a win over 100x multiplier", Icon = "⭐", ConditionType = "MaxMultiplier", ConditionValue = 100 },
            new Achievement { Id = Guid.NewGuid(), Name = "Veteran", Description = "Reach Level 10", Icon = "🎖️", ConditionType = "LevelReached", ConditionValue = 10 }
        );

        // Seed Default Settings
        modelBuilder.Entity<GlobalSetting>().HasData(
            new GlobalSetting { Key = "GlobalTargetRtp", Value = "95.0", Description = "Target RTP percentage for the system", LastUpdated = DateTime.UtcNow },
            new GlobalSetting { Key = "EmergencyStop", Value = "false", Description = "Master switch to pause all games", LastUpdated = DateTime.UtcNow },
            new GlobalSetting { Key = "VolatilityMode", Value = "Standard", Description = "Global volatility profile (Low, Standard, High)", LastUpdated = DateTime.UtcNow },
            new GlobalSetting { Key = "Content_Help", Value = "Welcome to AleaSim Help Center. Use the expansion panels below to find answers.", Description = "Help Page Introduction Content", LastUpdated = DateTime.UtcNow },
            new GlobalSetting { Key = "Content_Terms", Value = "By using AleaSim, you agree to our terms of service...", Description = "Terms of Service Content", LastUpdated = DateTime.UtcNow },
            new GlobalSetting { Key = "Content_Privacy", Value = "We value your privacy. Your data is encrypted...", Description = "Privacy Policy Content", LastUpdated = DateTime.UtcNow }
        );
    }
}