using AleaSim.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AleaSim.Persistence.Configurations;

public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent> {
    public void Configure(EntityTypeBuilder<AuditEvent> builder) {
        builder.HasKey(e => e.Id);
        
        builder.HasIndex(e => e.Timestamp);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.Hash).IsUnique();
        
        // Ensure append-only by throwing error on update is trickier in EF directly without triggers,
        // but we can map it to allow no modifications.
        // For strict enforcement, we rely on the repository logic, but here we define the schema.
        
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Hash).IsRequired();

        // Indexes for performance (Issue 22)
        builder.HasIndex(e => e.Timestamp);
        builder.HasIndex(e => e.EventType);
        builder.HasIndex(e => e.UserId);
    }
}
