using AleaSim.Domain.Entities;
using AleaSim.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AleaSim.Persistence;

public class AleaSimDbContext : DbContext {
    public DbSet<User> Users { get; set; }
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

    public AleaSimDbContext(DbContextOptions<AleaSimDbContext> options) : base(options) {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
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

        modelBuilder.Entity<GlobalSetting>(entity => {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(100);
            entity.Property(e => e.Value).HasMaxLength(500);
        });

        // Seed Default Settings
        modelBuilder.Entity<GlobalSetting>().HasData(
            new GlobalSetting { Key = "GlobalTargetRtp", Value = "95.0", Description = "Target RTP percentage for the system", LastUpdated = DateTime.UtcNow },
            new GlobalSetting { Key = "EmergencyStop", Value = "false", Description = "Master switch to pause all games", LastUpdated = DateTime.UtcNow },
            new GlobalSetting { Key = "VolatilityMode", Value = "Standard", Description = "Global volatility profile (Low, Standard, High)", LastUpdated = DateTime.UtcNow }
        );
    }
}