// c:\Develop\Mini-CDS\src\MiniCds.Domain\Abstractions\IReportRepository.cs
using MiniCds.Domain.Entities;

namespace MiniCds.Domain.Abstractions;

/// <summary>
/// Repository for Report entity persistence.
/// </summary>
public interface IReportRepository
{
    Task<long> AddAsync(Report report, CancellationToken ct = default);
    Task UpdateAsync(Report report, CancellationToken ct = default);
    Task<IReadOnlyList<Report>> GetAllAsync(CancellationToken ct = default);
}