// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Persistence\AuditTrailTests.cs
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniCds.Application.Audit;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;
using MiniCds.Infrastructure.Persistence;

namespace MiniCds.Tests.Persistence;

public class AuditTrailTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public AuditTrailTests()
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

    private async Task<long> SeedUserAsync(string username = "tester")
    {
        using var db = CreateContext();
        var user = new User
        {
            Username = username,
            Role = UserRole.Operator,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Append_FirstEntry_ChainsFromGenesis_AndVerifies()
    {
        long userId = await SeedUserAsync();
        using var db = CreateContext();
        var trail = new AuditTrail(db, new HashChain());

        long id = await trail.AppendAsync(
            AuditAction.Login, "User", userId, "session start",
            null, null, userId);

        id.Should().Be(1);
        (await trail.VerifyChainAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Append_ThreeEntries_IdsSequentialAndChainValid()
    {
        long userId = await SeedUserAsync();
        using var db = CreateContext();
        var trail = new AuditTrail(db, new HashChain());

        for (int i = 0; i < 3; i++)
        {
            long id = await trail.AppendAsync(
                AuditAction.ChangeMethod, "Method", 42, $"edit {i}",
                new { Param = i }, new { Param = i + 1 }, userId);
            id.Should().Be(i + 1);
        }

        (await trail.VerifyChainAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task VerifyChain_DetectsRawSqlHashTamper()
    {
        long userId = await SeedUserAsync();
        using (var db = CreateContext())
        {
            var trail = new AuditTrail(db, new HashChain());
            await trail.AppendAsync(AuditAction.Login, "User", userId, null, null, null, userId);
            await trail.AppendAsync(AuditAction.Sign, "Method", 1, "sign", null, null, userId);
        }

        // Attacker edits the first entry's hash directly in the DB
        // (append-only triggers are not in place yet — this simulates a sophisticated attacker).
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE audit_entries SET Hash = @h WHERE Id = 1";
            cmd.Parameters.AddWithValue("@h", new string('f', 64));
            cmd.ExecuteNonQuery();
        }

        using var verifyDb = CreateContext();
        var verifier = new AuditTrail(verifyDb, new HashChain());

        (await verifier.VerifyChainAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task QueryAsync_JoinsUsername_AndFiltersByAction()
    {
        long userId = await SeedUserAsync("alice");
        using var db = CreateContext();
        var trail = new AuditTrail(db, new HashChain());

        await trail.AppendAsync(AuditAction.Login, "User", userId, null, null, null, userId);
        await trail.AppendAsync(AuditAction.ExportReport, "Method", 7, "pdf",
            null, new { Format = "pdf" }, userId);

        var logins = await trail.QueryAsync(action: AuditAction.Login);
        var exports = await trail.QueryAsync(entityType: "Method");

        logins.Should().HaveCount(1);
        logins[0].ActorUsername.Should().Be("alice");
        exports.Should().HaveCount(1);
        exports[0].NewValues.Should().Contain("pdf");
    }
}