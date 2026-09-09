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

    public async Task<Sample?> FindByIdWithPeaksAsync(long id, CancellationToken ct = default)
        => await dbContext.Samples
            .Include(s => s.Method)
            .Include(s => s.Peaks)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Sample>> GetAllAsync(CancellationToken ct = default)
        => await dbContext.Samples
            .Include(s => s.Method)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<long> AddAsync(Sample sample, CancellationToken ct = default)
    {
        dbContext.Samples.Add(sample);
        await dbContext.SaveChangesAsync(ct);
        return sample.Id;
    }

    public async Task UpdateStatusAsync(long sampleId, SampleStatus newStatus, CancellationToken ct = default)
    {
        var sample = await dbContext.Samples.FindAsync([sampleId], ct)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");
        sample.Status = newStatus;
        await dbContext.SaveChangesAsync(ct);
    }
}