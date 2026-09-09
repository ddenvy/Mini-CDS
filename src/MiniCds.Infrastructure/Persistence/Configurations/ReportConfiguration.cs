// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\Configurations\ReportConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniCds.Domain.Entities;

namespace MiniCds.Infrastructure.Persistence.Configurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Title).HasMaxLength(256).IsRequired();
        builder.Property(r => r.FilePath).HasMaxLength(1024).IsRequired();
        builder.Property(r => r.Format).HasMaxLength(32).IsRequired();
        
        builder.Property(r => r.GeneratedAtUtc)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        
        builder.HasOne(r => r.GeneratedBy)
            .WithMany()
            .HasForeignKey(r => r.GeneratedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(r => r.GeneratedAtUtc);
    }
}