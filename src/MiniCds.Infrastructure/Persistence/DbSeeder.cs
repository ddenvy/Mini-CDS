// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\DbSeeder.cs
using Microsoft.EntityFrameworkCore;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;

namespace MiniCds.Infrastructure.Persistence;

/// <summary>Username of the non-interactive account that authors system audit events.</summary>
public static class WellKnownUsers
{
    public const string System = "system";
}

/// <summary>A user created by the seeder; Password is null when it came from configuration.</summary>
public sealed record SeededUser(string Username, long Id, string? Password);

/// <summary>Outcome of one seeding pass.</summary>
public sealed record SeedResult(long SystemUserId, IReadOnlyList<SeededUser> Created);

/// <summary>
/// Idempotently creates the non-interactive <c>system</c> account (author of system audit
/// events) and the demo operator/admin accounts. Existing users are never modified.
/// Seeding writes no audit entries: the system account it creates is the journal author,
/// so a self-referential first entry is impossible by construction.
/// </summary>
public sealed class DbSeeder(CdsDbContext db, IPasswordHasher passwordHasher)
{
    /// <summary>
    /// When <paramref name="demoPassword"/> is null, a random password is generated per created
    /// user and returned in the result — it is never persisted in plaintext.
    /// </summary>
    public async Task<SeedResult> SeedAsync(string? demoPassword, CancellationToken ct = default)
    {
        var existing = await db.Users.Select(u => u.Username).ToListAsync(ct);

        var created = new List<SeededUser>();
        long systemUserId = 0;

        if (!existing.Contains(WellKnownUsers.System, StringComparer.OrdinalIgnoreCase))
        {
            var system = new User
            {
                Username = WellKnownUsers.System,
                FullName = "System",
                // No credentials at all: interactive login is impossible by construction.
                PasswordHash = string.Empty,
                PasswordSalt = string.Empty,
                Role = UserRole.Administrator,
                IsActive = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Users.Add(system);
            await db.SaveChangesAsync(ct);
            systemUserId = system.Id;
            created.Add(new SeededUser(system.Username, system.Id, null));
        }
        else
        {
            systemUserId = await db.Users
                .Where(u => u.Username == WellKnownUsers.System)
                .Select(u => u.Id)
                .SingleAsync(ct);
        }

        foreach (var (username, fullName, role) in DemoAccounts())
        {
            if (existing.Contains(username, StringComparer.OrdinalIgnoreCase))
                continue;

            string? password = demoPassword ?? GeneratePassword();
            var (hash, salt) = passwordHasher.Hash(password);

            var user = new User
            {
                Username = username,
                FullName = fullName,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = role,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            // Report the plaintext only when we generated it; configured passwords stay private.
            created.Add(new SeededUser(username, user.Id, demoPassword is null ? password : null));
        }

        return new SeedResult(systemUserId, created);
    }

    private static IEnumerable<(string Username, string FullName, UserRole Role)> DemoAccounts()
    {
        yield return ("admin", "Demo Administrator", UserRole.Administrator);
        yield return ("operator", "Demo Operator", UserRole.Operator);
    }

    private static string GeneratePassword()
    {
        const string alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$%";
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        return new string(bytes.Select(b => alphabet[b % alphabet.Length]).ToArray());
    }
}
