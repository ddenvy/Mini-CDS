// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\Configurations\PeakConfiguration.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniCds.Domain.Entities;
using MiniCds.Domain.ValueObjects;

namespace MiniCds.Infrastructure.Persistence.Configurations;

public class PeakConfiguration : IEntityTypeConfiguration<Peak>
{
    public void Configure(EntityTypeBuilder<Peak> builder)
    {
        builder.ToTable("peaks");
        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Metrics)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<PeakMetrics>(v, (JsonSerializerOptions?)null)!)
            .HasColumnType("TEXT");
        
        builder.Property(p => p.ApexIndex).IsRequired();
        builder.Property(p => p.StartIndex).IsRequired();
        builder.Property(p => p.EndIndex).IsRequired();
        builder.Property(p => p.IsManual).IsRequired();
        
        builder.Property(p => p.DetectedAtUtc)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(p => p.VoidedAtUtc)
            .HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null);
        
        builder.HasOne(p => p.Sample)
            .WithMany(s => s.Peaks)
            .HasForeignKey(p => p.SampleId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(p => p.VoidedBy)
            .WithMany()
            .HasForeignKey(p => p.VoidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(p => p.SampleId);
    }
}