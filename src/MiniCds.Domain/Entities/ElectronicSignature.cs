using MiniCds.Domain.Enums;

namespace MiniCds.Domain.Entities;

///<summary>
/// Immutable electronic signature bound to a domain entity.
/// Signature carry legal meaning under FDA 21 CFR Part 11.
///<summary>
public sealed class ElectronicSignature
{
    public long Id { get; init; }
    public long UserId { get; init; }
    public SignatureMeaning Meaning { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
    public string LinkedEntityType { get; init; } = string.Empty;
    public long LinkedEntityId { get; init; }
    public long AuditEntryId { get; init; }
}