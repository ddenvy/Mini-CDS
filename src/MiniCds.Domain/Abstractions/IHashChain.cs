namespace MiniCds.Domain.Abstractions;

/// <summary>
/// SHA256 hash chain builder for tamper-evident audit trail.
/// GenesisPrevHash = fixed sentinel, each entry chains the previous hash.
/// </summary>
public interface IHashChain
{
    string ComputeHash(string id, string timestamp, string action, string actorId,
                       string entityType, string entityId, string? reason,
                       string? oldValues, string? newValues, string prevHash);
}