// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Persistence\SignatureServiceTests.cs
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniCds.Application.Audit;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;
using MiniCds.Infrastructure.Persistence;
using MiniCds.Infrastructure.Security;

namespace MiniCds.Tests.Persistence;

public class SignatureServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PasswordHasher _hasher = new();

    public SignatureServiceTests()
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

    private async Task<(long id, string username)> SeedUserAsync(
        string username = "signer", bool active = true)
    {
        var (hash, salt) = _hasher.Hash("Correct!Horse9");
        using var db = CreateContext();
        var user = new User
        {
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRole.Analyst,
            IsActive = active,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, user.Username);
    }

    private SignatureService CreateService(CdsDbContext db)
        => new(db, _hasher, new HashChain());

    [Fact]
    public async Task SignAsync_CorrectCredentials_CreatesSignatureWithCrossReferences()
    {
        var (userId, username) = await SeedUserAsync();
        using var db = CreateContext();
        var service = CreateService(db);

        var signature = await service.SignAsync(
            username, "Correct!Horse9", SignatureMeaning.Approved,
            "Method release", "Method", 42, userId);

        signature.Should().NotBeNull();
        signature!.Id.Should().Be(1);
        signature.AuditEntryId.Should().Be(1);

        var audit = db.AuditEntries.Single();
        audit.Id.Should().Be(1);
        audit.SignatureId.Should().Be(signature.Id);
        audit.Action.Should().Be(AuditAction.Sign);
        audit.EntityType.Should().Be("Method");
    }

    [Fact]
    public async Task SignAsync_ThenVerifyChain_IsValid()
    {
        var (userId, username) = await SeedUserAsync();
        using var db = CreateContext();
        var service = CreateService(db);

        await service.SignAsync(username, "Correct!Horse9", SignatureMeaning.Reviewed,
            "checked", "Method", 1, userId);

        var trail = new AuditTrail(db, new HashChain());
        (await trail.VerifyChainAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task SignAsync_WrongPassword_ReturnsNullAndWritesNothing()
    {
        var (userId, username) = await SeedUserAsync();
        using var db = CreateContext();
        var service = CreateService(db);

        var signature = await service.SignAsync(
            username, "wrong-password", SignatureMeaning.Approved,
            "x", "Method", 1, userId);

        signature.Should().BeNull();
        db.AuditEntries.Should().BeEmpty();
        db.ElectronicSignatures.Should().BeEmpty();
    }

    [Fact]
    public async Task SignAsync_UnknownUser_ReturnsNull()
    {
        var (userId, _) = await SeedUserAsync("someone_else");
        using var db = CreateContext();
        var service = CreateService(db);

        var signature = await service.SignAsync(
            "ghost", "whatever", SignatureMeaning.Approved, "x", "Method", 1, userId);

        signature.Should().BeNull();
    }

    [Fact]
    public async Task SignAsync_InactiveUser_ReturnsNull()
    {
        var (userId, username) = await SeedUserAsync("fired", active: false);
        using var db = CreateContext();
        var service = CreateService(db);

        var signature = await service.SignAsync(
            username, "Correct!Horse9", SignatureMeaning.Approved, "x", "Method", 1, userId);

        signature.Should().BeNull();
    }

    [Fact]
    public async Task SignAsync_ActeurMismatch_ReturnsNull()
    {
        var (_, username) = await SeedUserAsync("alice");
        var (otherId, _) = await SeedUserAsync("bob");
        using var db = CreateContext();
        var service = CreateService(db);

        // Bob tries to sign while re-authenticating as Alice
        var signature = await service.SignAsync(
            username, "Correct!Horse9", SignatureMeaning.Approved, "x", "Method", 1, otherId);

        signature.Should().BeNull();
    }

    [Fact]
    public async Task SignAsync_TwoSignatures_SequentialIdsAndSingleChain()
    {
        var (userId, username) = await SeedUserAsync();
        using var db = CreateContext();
        var service = CreateService(db);

        var first = await service.SignAsync(username, "Correct!Horse9",
            SignatureMeaning.Reviewed, "r1", "Method", 1, userId);
        var second = await service.SignAsync(username, "Correct!Horse9",
            SignatureMeaning.Approved, "r2", "Method", 1, userId);

        first!.Id.Should().Be(1);
        second!.Id.Should().Be(2);
        first.AuditEntryId.Should().Be(1);
        second.AuditEntryId.Should().Be(2);

        var trail = new AuditTrail(db, new HashChain());
        (await trail.VerifyChainAsync()).Should().BeTrue();
    }
}