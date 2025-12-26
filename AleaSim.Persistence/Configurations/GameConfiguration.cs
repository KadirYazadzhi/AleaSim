using AleaSim.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AleaSim.Persistence.Configurations;

public class GameConfiguration : IEntityTypeConfiguration<Game> {
    public void Configure(EntityTypeBuilder<Game> builder) {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).IsRequired().HasMaxLength(100);
        builder.Property(g => g.Type).IsRequired().HasMaxLength(50);
        builder.Property(g => g.MinBet).HasPrecision(18, 2);
        builder.Property(g => g.MaxBet).HasPrecision(18, 2);
    }
}
