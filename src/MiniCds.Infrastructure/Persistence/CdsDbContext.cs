// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\CdsDbContext.cs
using Microsoft.EntityFrameworkCore;
using MiniCds.Domain.Entities;

namespace MiniCds.Infrastructure.Persistence;

/// <summary>
/// SQLite-backed persistence context for the CDS audit and identity subsystem.
/// </summary>
public class CdsDbContext(DbContextOptions<CdsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<ElectronicSignature> ElectronicSignatures => Set<ElectronicSignature>();
    public DbSet<Method> Methods => Set<Method>();
    public DbSet<Sample> Samples => Set<Sample>();
    public DbSet<RawSignal> RawSignals => Set<RawSignal>();
    public DbSet<Peak> Peaks => Set<Peak>();
    public DbSet<Report> Reports => Set<Report>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new AppendOnlyInterceptor());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CdsDbContext).Assembly);
    }
}