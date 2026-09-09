// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\Configurations\RawSignalConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniCds.Domain.Entities;

namespace MiniCds.Infrastructure.Persistence.Configurations;

public class RawSignalConfiguration : IEntityTypeConfiguration<RawSignal>
{
    public void Configure(EntityTypeBuilder<RawSignal> builder)
    {
        builder.ToTable("raw_signals");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.SampleRateHz).IsRequired();
        builder.Property(r => r.Points).IsRequired();
        
        builder.Property(r => r.CapturedAtUtc)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        
        builder.HasOne(r => r.Sample)
            .WithMany(s => s.RawSignals)
            .HasForeignKey(r => r.SampleId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(r => r.SampleId);
    }
}