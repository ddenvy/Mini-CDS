// c:\Develop\Mini-CDS\src\MiniCds.Domain\Abstractions\IUserStore.cs
using MiniCds.Domain.Entities;

namespace MiniCds.Domain.Abstractions;

/// <summary>
/// Read access to user accounts for authentication. Deliberately minimal:
/// login only needs lookup by username and the system account id.
/// </summary>
public interface IUserStore
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>Id of the non-interactive system account that authors system audit events.</summary>
    Task<long> GetSystemUserIdAsync(CancellationToken ct = default);
}
