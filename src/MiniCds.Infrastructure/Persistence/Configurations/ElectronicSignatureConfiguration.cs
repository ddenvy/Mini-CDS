using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniCds.Domain.Entities;

namespace MiniCds.Infrastructure.Persistence.Configurations;

public class ElectronicSignatureConfiguration : IEntityTypeConfiguration<ElectronicSignature>
{
    public void Configure(EntityTypeBuilder<ElectronicSignature> builder)
    {
        builder.ToTable("electronic_signatures");
        builder.HasKey(s => s.Id);

        // Id is allocated deterministically (max+1) inside the signing transaction:
        // AuditEntry.SignatureId must be known at insert time, and append-only triggers
        // forbid a follow-up UPDATE to fill the cross-reference.
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Meaning).HasConversion<string>().HasMaxLength(32);
        builder.Property(s => s.Reason).HasMaxLength(1024).IsRequired();
        builder.Property(s => s.LinkedEntityType).HasMaxLength(64).IsRequired();
        builder.Property(s => s.TimestampUtc)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Signature → AuditEntry: the only declared side of the signature↔audit link.
        // AuditEntry.SignatureId remains a plain column (no FK) to avoid a cycle.
        builder.HasOne<AuditEntry>()
            .WithMany()
            .HasForeignKey(s => s.AuditEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Primary lookup: "all signatures for entity X"
        builder.HasIndex(s => new { s.LinkedEntityType, s.LinkedEntityId });
    }
}