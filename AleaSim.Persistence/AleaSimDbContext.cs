using AleaSim.Domain.Entities;
using AleaSim.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AleaSim.Persistence;

public class AleaSimDbContext : DbContext {
    public AleaSimDbContext(DbContextOptions<AleaSimDbContext> options) : base(options) {
    }

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

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AleaSimDbContext).Assembly);
    }
}
