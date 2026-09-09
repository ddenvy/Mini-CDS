// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\Configurations\SampleConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniCds.Domain.Entities;

namespace MiniCds.Infrastructure.Persistence.Configurations;

public class SampleConfiguration : IEntityTypeConfiguration<Sample>
{
    public void Configure(EntityTypeBuilder<Sample> builder)
    {
        builder.ToTable("samples");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(128).IsRequired();
        
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(32);
        
        builder.Property(s => s.CreatedAtUtc)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(s => s.VoidedAtUtc)
            .HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null);
        
        builder.HasOne(s => s.Method)
            .WithMany(m => m.Samples)
            .HasForeignKey(s => s.MethodId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(s => s.CreatedBy)
            .WithMany()
            .HasForeignKey(s => s.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(s => s.VoidedBy)
            .WithMany()
            .HasForeignKey(s => s.VoidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(s => s.Status);
    }
}