using AleaSim.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AleaSim.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User> {
    public void Configure(EntityTypeBuilder<User> builder) {
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => u.Username).IsUnique();
        
        builder.Property(u => u.Username).IsRequired().HasMaxLength(50);
        builder.Property(u => u.Email).HasMaxLength(100);
        builder.Property(u => u.PasswordHash).HasMaxLength(255);
        
        builder.Property(u => u.Balance).HasPrecision(18, 2);
    }
}
