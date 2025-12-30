using AleaSim.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AleaSim.Persistence.Configurations;

public class PlayerProfileConfiguration : IEntityTypeConfiguration<PlayerProfile> {
    public void Configure(EntityTypeBuilder<PlayerProfile> builder) {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TotalWagered).HasColumnType("decimal(18,2)");
        builder.Property(x => x.TotalPaid).HasColumnType("decimal(18,2)");
        builder.Property(x => x.NetDeposit).HasColumnType("decimal(18,2)");
        builder.Property(x => x.CurrentSessionRtp).HasColumnType("decimal(18,2)");

        // 1:1 Relationship with User
        builder.HasOne(x => x.User)
               .WithOne()
               .HasForeignKey<PlayerProfile>(x => x.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
