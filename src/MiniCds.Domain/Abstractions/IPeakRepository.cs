// c:\Develop\Mini-CDS\src\MiniCds.Domain\Abstractions\IPeakRepository.cs
using MiniCds.Domain.Entities;

namespace MiniCds.Domain.Abstractions;

/// <summary>
/// Repository for Peak entity persistence.
/// </summary>
public interface IPeakRepository
{
    Task AddAsync(Peak peak, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Peak> peaks, CancellationToken ct = default);
}
