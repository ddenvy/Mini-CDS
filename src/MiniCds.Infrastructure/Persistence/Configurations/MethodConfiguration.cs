// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\Configurations\MethodConfiguration.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniCds.Domain.Entities;
using MiniCds.Domain.ValueObjects;

namespace MiniCds.Infrastructure.Persistence.Configurations;

public class MethodConfiguration : IEntityTypeConfiguration<Method>
{
    public void Configure(EntityTypeBuilder<Method> builder)
    {
        builder.ToTable("methods");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).HasMaxLength(128).IsRequired();
        builder.Property(m => m.Version).IsRequired();
        
        builder.Property(m => m.Parameters)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<ProcessingParameters>(v, (JsonSerializerOptions?)null)!)
            .HasColumnType("TEXT");
        
        builder.Property(m => m.CreatedAtUtc)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        
        builder.HasOne(m => m.CreatedBy)
            .WithMany()
            .HasForeignKey(m => m.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(m => new { m.Name, m.Version }).IsUnique();
    }
}