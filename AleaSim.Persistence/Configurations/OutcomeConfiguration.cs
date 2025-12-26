using AleaSim.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AleaSim.Persistence.Configurations;

public class OutcomeConfiguration : IEntityTypeConfiguration<Outcome> {
    public void Configure(EntityTypeBuilder<Outcome> builder) {
        builder.HasKey(o => o.Id);
        builder.HasIndex(o => o.GameRoundId);
        builder.Property(o => o.WinAmount).HasPrecision(18, 2);
    }
}
