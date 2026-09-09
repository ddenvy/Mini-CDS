// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\Repositories\PeakRepository.cs
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;

namespace MiniCds.Infrastructure.Persistence.Repositories;

public sealed class PeakRepository(CdsDbContext dbContext) : IPeakRepository
{
    public async Task AddAsync(Peak peak, CancellationToken ct = default)
    {
        dbContext.Peaks.Add(peak);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<Peak> peaks, CancellationToken ct = default)
    {
        dbContext.Peaks.AddRange(peaks);
        await dbContext.SaveChangesAsync(ct);
    }
}