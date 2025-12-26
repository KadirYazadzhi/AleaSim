using AleaSim.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AleaSim.Persistence.Configurations;

public class BetConfiguration : IEntityTypeConfiguration<Bet> {
    public void Configure(EntityTypeBuilder<Bet> builder) {
        builder.HasKey(b => b.Id);
        builder.HasIndex(b => b.GameRoundId);
        builder.Property(b => b.Amount).HasPrecision(18, 2);
    }
}

public class GameRoundConfiguration : IEntityTypeConfiguration<GameRound> {
    public void Configure(EntityTypeBuilder<GameRound> builder) {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.GameSessionId);
        builder.HasIndex(r => r.ExecutedAt);
        
        builder.Property(r => r.TotalBetAmount).HasPrecision(18, 2);
        builder.Property(r => r.TotalWinAmount).HasPrecision(18, 2);
    }
}
