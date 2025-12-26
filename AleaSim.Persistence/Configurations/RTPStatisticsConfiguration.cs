using AleaSim.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AleaSim.Persistence.Configurations;

public class RTPStatisticsConfiguration : IEntityTypeConfiguration<RTPStatistics> {
    public void Configure(EntityTypeBuilder<RTPStatistics> builder) {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.GameId);
        builder.HasIndex(s => s.UserId);
        
        builder.Property(s => s.TotalWagered).HasPrecision(18, 2);
        builder.Property(s => s.TotalPaid).HasPrecision(18, 2);
    }
}
