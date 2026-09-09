// c:\Develop\Mini-CDS\src\MiniCds.Domain\Abstractions\ISampleRepository.cs
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;

namespace MiniCds.Domain.Abstractions;

/// <summary>
/// Repository for Sample entity lifecycle management.
/// </summary>
public interface ISampleRepository
{
    Task<Sample?> FindByIdAsync(long id, CancellationToken ct = default);
    Task UpdateStatusAsync(long sampleId, SampleStatus newStatus, CancellationToken ct = default);
    Task<IReadOnlyList<Sample>> GetAllAsync(CancellationToken ct = default);
}
