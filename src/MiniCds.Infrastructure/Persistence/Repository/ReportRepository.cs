// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\Repositories\ReportRepository.cs
using Microsoft.EntityFrameworkCore;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;

namespace MiniCds.Infrastructure.Persistence.Repositories;

public sealed class ReportRepository(CdsDbContext dbContext) : IReportRepository
{
    public async Task<long> AddAsync(Report report, CancellationToken ct = default)
    {
        dbContext.Reports.Add(report);
        await dbContext.SaveChangesAsync(ct);
        return report.Id;
    }

    public async Task UpdateAsync(Report report, CancellationToken ct = default)
    {
        dbContext.Reports.Update(report);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Report>> GetAllAsync(CancellationToken ct = default)
        => await dbContext.Reports
            .OrderByDescending(r => r.GeneratedAtUtc)
            .ToListAsync(ct);
}