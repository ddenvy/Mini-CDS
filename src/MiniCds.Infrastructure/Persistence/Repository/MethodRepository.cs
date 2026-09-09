// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\Repositories\MethodRepository.cs
using Microsoft.EntityFrameworkCore;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;

namespace MiniCds.Infrastructure.Persistence.Repositories;

public sealed class MethodRepository(CdsDbContext dbContext) : IMethodRepository
{
    public async Task<Method?> FindByIdAsync(long id, CancellationToken ct = default)
        => await dbContext.Methods.FindAsync([id], ct);

    public async Task<IReadOnlyList<Method>> GetAllAsync(CancellationToken ct = default)
        => await dbContext.Methods.OrderBy(m => m.Id).ToListAsync(ct);
}