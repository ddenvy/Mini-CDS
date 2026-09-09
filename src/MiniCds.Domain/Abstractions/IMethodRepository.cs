// c:\Develop\Mini-CDS\src\MiniCds.Domain\Abstractions\IMethodRepository.cs
using MiniCds.Domain.Entities;

namespace MiniCds.Domain.Abstractions;

/// <summary>
/// Repository for Method entity access.
/// </summary>
public interface IMethodRepository
{
    Task<Method?> FindByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<Method>> GetAllAsync(CancellationToken ct = default);
}
