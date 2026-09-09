// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\Repositories\RawSignalRepository.cs
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;

namespace MiniCds.Infrastructure.Persistence.Repositories;

public sealed class RawSignalRepository(CdsDbContext dbContext) : IRawSignalRepository
{
    public async Task AddAsync(RawSignal rawSignal, CancellationToken ct = default)
    {
        dbContext.RawSignals.Add(rawSignal);
        await dbContext.SaveChangesAsync(ct);
    }
}