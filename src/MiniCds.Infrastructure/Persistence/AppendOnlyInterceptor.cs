// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\AppendOnlyInterceptor.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MiniCds.Domain.Entities;

namespace MiniCds.Infrastructure.Persistence;

/// <summary>
/// First line of defense: rejects any EF attempt to Modify or Delete append-only entities
/// before SQL is ever generated. Physical deletion/modification is forbidden under 21 CFR Part 11.
/// </summary>
public sealed class AppendOnlyInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Guard(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditEntry or ElectronicSignature &&
                entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} is append-only: modification and deletion are forbidden.");
            }
        }
    }
}
