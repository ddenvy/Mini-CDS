// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Persistence\DbSeederTests.cs
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniCds.Application.Audit;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;
using MiniCds.Infrastructure.Persistence;
using MiniCds.Infrastructure.Security;

namespace MiniCds.Tests.Persistence;

public class DbSeederTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PasswordHasher _hasher = new();

    public DbSeederTests()
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

    private DbSeeder CreateSeeder(CdsDbContext db) => new(db, _hasher);

    [Fact]
    public async Task Seed_CreatesSystemAccount_WithoutCredentialsAndInactive()
    {
        using var db = CreateContext();

        var result = await CreateSeeder(db).SeedAsync("Lab!Password1");

        var system = db.Users.Single(u => u.Username == WellKnownUsers.System);
        system.IsActive.Should().BeFalse();
        system.PasswordHash.Should().BeEmpty();
        system.PasswordSalt.Should().BeEmpty();
        result.SystemUserId.Should().Be(system.Id);
    }

    [Fact]
    public async Task Seed_CreatesDemoUsers_WithConfiguredPassword()
    {
        using var db = CreateContext();

        var result = await CreateSeeder(db).SeedAsync("Lab!Password1");

        db.Users.Count().Should().Be(3);
        db.Users.Count(u => u.IsActive).Should().Be(2);
        result.Created.Should().NotContain(c => c.Password != null);

        var admin = db.Users.Single(u => u.Username == "admin");
        _hasher.Verify("Lab!Password1", admin.PasswordHash, admin.PasswordSalt).Should().BeTrue();
    }

    [Fact]
    public async Task Seed_WithoutConfiguredPassword_GeneratesVerifiableDistinctPasswords()
    {
        using var db = CreateContext();

        var result = await CreateSeeder(db).SeedAsync(demoPassword: null);

        var credentials = result.Created.Where(c => c.Password is not null).ToList();
        credentials.Should().HaveCount(2);
        credentials.Select(c => c.Password).Should().OnlyHaveUniqueItems();

        foreach (var cred in credentials)
        {
            var user = db.Users.Single(u => u.Username == cred.Username);
            _hasher.Verify(cred.Password!, user.PasswordHash, user.PasswordSalt).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Seed_Twice_IsIdempotent_AndDoesNotTouchExistingUsers()
    {
        using (var db = CreateContext())
        {
            await CreateSeeder(db).SeedAsync("First!Password");
        }

        string storedHash;
        using (var db = CreateContext())
        {
            storedHash = db.Users.Single(u => u.Username == "admin").PasswordHash;
        }

        using (var db = CreateContext())
        {
            var second = await CreateSeeder(db).SeedAsync("Second!Password");

            second.Created.Should().BeEmpty();
            second.SystemUserId.Should().BeGreaterThan(0);
        }

        using (var db = CreateContext())
        {
            db.Users.Count().Should().Be(3);
            db.Users.Single(u => u.Username == "admin").PasswordHash.Should().Be(storedHash);
        }
    }

    [Fact]
    public async Task SystemAccount_CannotSign_EvenWithAnyPassword()
    {
        long systemId;
        using (var db = CreateContext())
        {
            var result = await CreateSeeder(db).SeedAsync("Lab!Password1");
            systemId = result.SystemUserId;
        }

        using var signDb = CreateContext();
        var service = new SignatureService(signDb, _hasher, new HashChain());

        var signature = await service.SignAsync(
            WellKnownUsers.System, string.Empty, SignatureMeaning.Approved,
            "attempt", "Method", 1, systemId);

        signature.Should().BeNull();
        signDb.ElectronicSignatures.Should().BeEmpty();
    }

    [Fact]
    public async Task SeededOperator_CanSign_AndChainStaysValid()
    {
        long operatorId;
        using (var db = CreateContext())
        {
            var result = await CreateSeeder(db).SeedAsync("Lab!Password1");
            operatorId = result.Created.Single(c => c.Username == "operator").Id;
        }

        using var signDb = CreateContext();
        var service = new SignatureService(signDb, _hasher, new HashChain());

        var signature = await service.SignAsync(
            "operator", "Lab!Password1", SignatureMeaning.Approved,
            "release", "Method", 7, operatorId);

        signature.Should().NotBeNull();
        (await new AuditTrail(signDb, new HashChain()).VerifyChainAsync()).Should().BeTrue();
    }
}
