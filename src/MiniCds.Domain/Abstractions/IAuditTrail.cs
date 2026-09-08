using MiniCds.Domain.Enums;

namespace MiniCds.Domain.Abstractions;

/// <summary>
/// Append-only audit trail. Non-negotiable under 21 CFR Part 11.
/// </summary>
public interface IAuditTrail
{
    Task<long> AppendAsync(
        AuditAction action,
        string entityType,
        long entityId,
        string? reason,
        object? oldValues,
        object? newValues,
        long actorUserId,
        long? signatureId = null);

    Task<bool> VerifyChainAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AuditEntryQueryRow>> QueryAsync(
        string? entityType = null,
        long? entityId = null,
        AuditAction? action = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);
}

/// <summary>Projection row for UI audit grid.</summary>
public readonly record struct AuditEntryQueryRow(
    long Id,
    DateTime TimestampUtc,
    string ActorUsername,
    AuditAction Action,
    string EntityType,
    long EntityId,
    string? Reason,
    string? OldValues,
    string? NewValues,
    long? SignatureId);