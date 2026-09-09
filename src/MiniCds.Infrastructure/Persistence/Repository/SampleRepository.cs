// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\Repositories\SampleRepository.cs
using Microsoft.EntityFrameworkCore;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;

namespace MiniCds.Infrastructure.Persistence.Repositories;

public sealed class SampleRepository(CdsDbContext dbContext) : ISampleRepository
{
    public async Task<Sample?> FindByIdAsync(long id, CancellationToken ct = default)
        => await dbContext.Samples.FindAsync([id], ct);

    public async Task UpdateStatusAsync(long sampleId, SampleStatus newStatus, CancellationToken ct = default)
    {
        var sample = await dbContext.Samples.FindAsync([sampleId], ct)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");
        sample.Status = newStatus;
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Sample>> GetAllAsync(CancellationToken ct = default)
        => await dbContext.Samples.AsNoTracking().ToListAsync(ct);
}