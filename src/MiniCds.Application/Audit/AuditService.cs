// c:\Develop\Mini-CDS\src\MiniCds.Application\Audit\AuditService.cs
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Enums;

namespace MiniCds.Application.Audit;

/// <summary>
/// Application-layer facade over the audit trail: validates audit requests and exposes
/// chain verification/querying to the UI without leaking IAuditTrail into ViewModels.
/// </summary>
public sealed class AuditService(IAuditTrail auditTrail)
{
    /// <summary>
    /// Records one auditable action. Throws before reaching the trail on invalid input.
    /// </summary>
    public Task<long> RecordAsync(
        AuditAction action,
        string entityType,
        long entityId,
        string? reason,
        object? oldValues,
        object? newValues,
        long actorUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("entityType must not be blank.", nameof(entityType));
        if (actorUserId <= 0)
            throw new ArgumentException("actorUserId must be a positive user id.", nameof(actorUserId));

        return auditTrail.AppendAsync(action, entityType, entityId, reason,
                                      oldValues, newValues, actorUserId);
    }

    /// <summary>Recomputes the whole hash chain; false means the journal was tampered with.</summary>
    public Task<bool> VerifyIntegrityAsync(CancellationToken ct = default)
        => auditTrail.VerifyChainAsync(ct);

    /// <summary>Filtered audit journal for the UI grid.</summary>
    public Task<IReadOnlyList<AuditEntryQueryRow>> QueryLogAsync(
        string? entityType = null,
        long? entityId = null,
        AuditAction? action = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
        => auditTrail.QueryAsync(entityType, entityId, action, from, to, ct);
}