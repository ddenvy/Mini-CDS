// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Persistence\EfUserStoreTests.cs
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniCds.Application.Audit;
using MiniCds.Infrastructure.Persistence;
using MiniCds.Infrastructure.Security;

namespace MiniCds.Tests.Persistence;

public class EfUserStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public EfUserStoreTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = CreateContext();
        db.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    private CdsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CdsDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CdsDbContext(options);
    }

    [Fact]
    public async Task FindByUsername_ReturnsSeededUser_AndSystemIdResolves()
    {
        using (var db = CreateContext())
        {
            var seeder = new DbSeeder(db, new PasswordHasher());
            await seeder.SeedAsync("Lab!Password1");
        }

        using var readDb = CreateContext();
        var store = new EfUserStore(readDb);

        var admin = await store.FindByUsernameAsync("admin");
        admin.Should().NotBeNull();
        admin!.IsActive.Should().BeTrue();

        (await store.GetSystemUserIdAsync()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSystemUserId_WithoutSeeding_Throws()
    {
        using var db = CreateContext();
        var store = new EfUserStore(db);

        var act = () => store.GetSystemUserIdAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
