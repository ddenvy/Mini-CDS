// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\Configurations\AuditEntryConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniCds.Domain.Entities;

namespace MiniCds.Infrastructure.Persistence.Configurations;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Reason).HasMaxLength(1024);
        builder.Property(a => a.TimestampUtc)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        // Actor → User: no navigation property on either side, so the FK must be declared explicitly
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // AuditEntry ← ElectronicSignature is owned by the signature side (see ElectronicSignatureConfiguration).
        // SignatureId stays a plain denormalized column (no FK) to avoid a second relationship and a cascade cycle.
    }
}