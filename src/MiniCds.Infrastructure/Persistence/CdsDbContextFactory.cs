// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\CdsDbContextFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MiniCds.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core tooling (migrations, database update).
/// Not used at runtime — the WPF host builds options via DI.
/// </summary>
public sealed class CdsDbContextFactory : IDesignTimeDbContextFactory<CdsDbContext>
{
    public CdsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CdsDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new CdsDbContext(options);
    }
}