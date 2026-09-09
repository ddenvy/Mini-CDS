// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\AuditTrail.cs
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniCds.Application.Audit;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;

namespace MiniCds.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed append-only audit trail with SHA256 hash chaining (21 CFR Part 11).
/// Ids are assigned deterministically (max+1 inside a transaction) because the id is
/// part of the hashed payload and must be known before the row is inserted.
/// </summary>
public sealed class AuditTrail(CdsDbContext db, IHashChain hashChain) : IAuditTrail
{
    public async Task<long> AppendAsync(
        AuditAction action,
        string entityType,
        long entityId,
        string? reason,
        object? oldValues,
        object? newValues,
        long actorUserId,
        long? signatureId = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        // Single writer per SQLite file; transaction makes max+1 race-free.
        var last = await db.AuditEntries.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Select(a => new { a.Id, a.Hash })
            .FirstOrDefaultAsync();

        long id = (last?.Id ?? 0) + 1;
        string prevHash = last?.Hash ?? HashChain.GenesisPrevHash;

        string? oldJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues);
        string? newJson = newValues is null ? null : JsonSerializer.Serialize(newValues);
        var timestampUtc = DateTime.UtcNow;

        string hash = hashChain.ComputeHash(
            id.ToString(CultureInfo.InvariantCulture),
            timestampUtc.ToString("O", CultureInfo.InvariantCulture),
            action.ToString(),
            actorUserId.ToString(CultureInfo.InvariantCulture),
            entityType,
            entityId.ToString(CultureInfo.InvariantCulture),
            reason, oldJson, newJson, prevHash);

        db.AuditEntries.Add(new AuditEntry
        {
            Id = id,
            TimestampUtc = timestampUtc,
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Reason = reason,
            OldValues = oldJson,
            NewValues = newJson,
            SignatureId = signatureId,
            PrevHash = prevHash,
            Hash = hash
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return id;
    }

    public async Task<bool> VerifyChainAsync(CancellationToken ct = default)
    {
        var entries = await db.AuditEntries.AsNoTracking()
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

        string expectedPrev = HashChain.GenesisPrevHash;
        foreach (var e in entries)
        {
            if (e.PrevHash != expectedPrev)
                return false;

            string recomputed = hashChain.ComputeHash(
                e.Id.ToString(CultureInfo.InvariantCulture),
                e.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                e.Action.ToString(),
                e.ActorUserId.ToString(CultureInfo.InvariantCulture),
                e.EntityType,
                e.EntityId.ToString(CultureInfo.InvariantCulture),
                e.Reason, e.OldValues, e.NewValues, e.PrevHash);

            if (recomputed != e.Hash)
                return false;

            expectedPrev = e.Hash;
        }

        return true;
    }

    public async Task<IReadOnlyList<AuditEntryQueryRow>> QueryAsync(
        string? entityType = null,
        long? entityId = null,
        AuditAction? action = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        IQueryable<AuditEntry> q = db.AuditEntries.AsNoTracking();
        if (entityType is not null) q = q.Where(a => a.EntityType == entityType);
        if (entityId is not null) q = q.Where(a => a.EntityId == entityId);
        if (action is not null) q = q.Where(a => a.Action == action.Value);
        if (from is not null) q = q.Where(a => a.TimestampUtc >= from.Value);
        if (to is not null) q = q.Where(a => a.TimestampUtc <= to.Value);

        // No navigation properties by design — username requires an explicit join.
        return await (from a in q
                      join u in db.Users.AsNoTracking() on a.ActorUserId equals u.Id
                      orderby a.Id
                      select new AuditEntryQueryRow(
                          a.Id, a.TimestampUtc, u.Username, a.Action,
                          a.EntityType, a.EntityId, a.Reason,
                          a.OldValues, a.NewValues, a.SignatureId))
            .ToListAsync(ct);
    }
}