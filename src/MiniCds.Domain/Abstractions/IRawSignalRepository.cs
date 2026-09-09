// c:\Develop\Mini-CDS\src\MiniCds.Domain\Abstractions\IRawSignalRepository.cs
using MiniCds.Domain.Entities;

namespace MiniCds.Domain.Abstractions;

/// <summary>
/// Repository for RawSignal entity persistence.
/// </summary>
public interface IRawSignalRepository
{
    Task AddAsync(RawSignal rawSignal, CancellationToken ct = default);
}
