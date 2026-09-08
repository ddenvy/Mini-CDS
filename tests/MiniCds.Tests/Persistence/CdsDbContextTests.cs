using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;
using MiniCds.Infrastructure.Persistence;

namespace MiniCds.Tests.Persistence;

/// <summary>
/// Round-trip tests against real SQLite (in-memory). Uses the actual migration SQL,
/// so schema, enum-as-string conversion, UTC normalization and FK constraints are all exercised.
/// </summary>
public class CdsDbContextTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CdsDbContextTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open(); // in-memory DB lives only while connection is open
    }

    public void Dispose() => _connection.Dispose();

    private CdsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CdsDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CdsDbContext(options);
    }

    private void ApplyMigrations()
    {
        using var db = CreateContext();
        db.Database.Migrate(); // executes real migration SQL, not EnsureCreated
    }

    [Fact]
    public void User_RoundTrip_PreservesEnumAsStringAndUtcKind()
    {
        ApplyMigrations();

        var created = new User
        {
            Username = "operator1",
            FullName = "Ivan Petrov",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Role = UserRole.Operator,
            IsActive = true,
            CreatedAtUtc = new DateTime(2026, 9, 8, 12, 30, 0, DateTimeKind.Utc)
        };

        using (var db = CreateContext())
        {
            db.Users.Add(created);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            var loaded = db.Users.Single(u => u.Username == "operator1");

            loaded.Role.Should().Be(UserRole.Operator);
            loaded.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
            loaded.CreatedAtUtc.Should().Be(created.CreatedAtUtc);
        }

        // Enum stored as TEXT (forensic readability) — verify at raw SQL level
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Role FROM users WHERE Username = 'operator1'";
            cmd.ExecuteScalar().Should().Be("Operator");
        }
    }

    [Fact]
    public void User_DuplicateUsername_Throws()
    {
        ApplyMigrations();

        using var db = CreateContext();
        db.Users.Add(new User { Username = "dup", Role = UserRole.Operator, IsActive = true });
        db.Users.Add(new User { Username = "dup", Role = UserRole.Analyst, IsActive = true });

        var act = () => db.SaveChanges();

        act.Should().Throw<DbUpdateException>();
    }

    [Fact]
    public void AuditAndSignature_RoundTrip_AndForeignKeyEnforced()
    {
        ApplyMigrations();

        long userId;
        using (var db = CreateContext())
        {
            var user = new User
            {
                Username = "admin",
                Role = UserRole.Administrator,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Users.Add(user);
            db.SaveChanges();
            userId = user.Id;

            var audit = new AuditEntry
            {
                TimestampUtc = DateTime.UtcNow,
                ActorUserId = userId,
                Action = AuditAction.Login,
                EntityType = "User",
                EntityId = userId,
                PrevHash = new string('0', 64),
                Hash = new string('a', 64)
            };
            db.AuditEntries.Add(audit);
            db.SaveChanges();

            db.ElectronicSignatures.Add(new ElectronicSignature
            {
                UserId = userId,
                Meaning = SignatureMeaning.Approved,
                Reason = "Method release",
                TimestampUtc = DateTime.UtcNow,
                LinkedEntityType = "Method",
                LinkedEntityId = 1,
                AuditEntryId = audit.Id
            });
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            db.AuditEntries.Should().HaveCount(1);
            var signature = db.ElectronicSignatures.Single();
            signature.Meaning.Should().Be(SignatureMeaning.Approved);
            signature.Reason.Should().Be("Method release");
        }

        // FK: audit entry referencing a nonexistent user must be rejected by SQLite
        using (var db = CreateContext())
        {
            db.AuditEntries.Add(new AuditEntry
            {
                TimestampUtc = DateTime.UtcNow,
                ActorUserId = 99999,
                Action = AuditAction.Login,
                EntityType = "User",
                EntityId = 1,
                PrevHash = string.Empty,
                Hash = string.Empty
            });

            var act = () => db.SaveChanges();

            act.Should().Throw<DbUpdateException>()
                .WithInnerException<Microsoft.Data.Sqlite.SqliteException>();
        }
    }
}