// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\Configurations\UserConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniCds.Domain.Entities;

namespace MiniCds.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).HasMaxLength(64).IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();

        // Enums as text: forensic-readable audit exports and hand-inspection of the DB
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(32);

        // SQLite returns DateTime with Kind=Unspecified — normalize back to Utc
        builder.Property(u => u.CreatedAtUtc)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    }
}