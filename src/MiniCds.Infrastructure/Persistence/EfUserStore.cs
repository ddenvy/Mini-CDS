// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\EfUserStore.cs
using Microsoft.EntityFrameworkCore;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;

namespace MiniCds.Infrastructure.Persistence;

public sealed class EfUserStore(CdsDbContext db) : IUserStore
{
    public Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default)
        => db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task<long> GetSystemUserIdAsync(CancellationToken ct = default)
    {
        long id = await db.Users.AsNoTracking()
            .Where(u => u.Username == WellKnownUsers.System)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        // Fail closed: without the system account there is no valid audit author.
        return id > 0
            ? id
            : throw new InvalidOperationException(
                $"System account '{WellKnownUsers.System}' is missing. Run seeding before authentication.");
    }
}
