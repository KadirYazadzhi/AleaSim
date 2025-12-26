using AleaSim.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AleaSim.Persistence.Configurations;

public class JackpotConfiguration : IEntityTypeConfiguration<Jackpot> {
    public void Configure(EntityTypeBuilder<Jackpot> builder) {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.CurrentValue).HasPrecision(18, 2);
        builder.Property(j => j.ContributionRate).HasPrecision(18, 5); // Need more precision for rates
        
        // Optimistic Concurrency Token
        builder.Property(j => j.LastUpdated).IsConcurrencyToken();
    }
}
