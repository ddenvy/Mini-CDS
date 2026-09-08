using MiniCds.Domain.Enums;

namespace MiniCds.Domain.Entities;

/// <summary>
/// Append-only audit trail entry. Physical deletion and modification are forbidden.
/// Hash chain provides tamper evidence (21 CFR Part 11).
///<summary>
public sealed class AuditEntry
{
    public long Id { get; init; }
    public DateTime TimestampUtc { get; init; }
    public long ActorUserId { get; init; }
    public AuditAction Action { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public long EntityId { get; init; }
    public string? Reason { get; init; }
    public string? OldValues { get; init; }
    public string? NewValues { get; init; }
    public long? SignatureId { get; init; }
    public string PrevHash { get; init; } = string.Empty;
    public string Hash { get; init; } = string.Empty;
}