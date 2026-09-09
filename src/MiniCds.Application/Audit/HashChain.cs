// c:\Develop\Mini-CDS\src\MiniCds.Application\Audit\HashChain.cs
using System.Security.Cryptography;
using System.Text;
using MiniCds.Domain.Abstractions;

namespace MiniCds.Application.Audit;

/// <summary>
/// SHA256 hash chain for the tamper-evident audit trail (21 CFR Part 11).
/// Fields are length-prefixed before hashing so that no delimiter sequence inside a
/// field value can shift content across a field boundary and produce a colliding hash.
/// </summary>
public sealed class HashChain : IHashChain
{
    /// <summary>Sentinel PrevHash of the first (genesis) audit entry.</summary>
    public const string GenesisPrevHash = "GENESIS";

    /// <inheritdoc />
    public string ComputeHash(string id, string timestamp, string action, string actorId,
                              string entityType, string entityId, string? reason,
                              string? oldValues, string? newValues, string prevHash)
    {
        var payload = new StringBuilder();
        AppendField(payload, id);
        AppendField(payload, timestamp);
        AppendField(payload, action);
        AppendField(payload, actorId);
        AppendField(payload, entityType);
        AppendField(payload, entityId);
        AppendField(payload, reason);
        AppendField(payload, oldValues);
        AppendField(payload, newValues);
        AppendField(payload, prevHash);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void AppendField(StringBuilder sb, string? value)
    {
        value ??= string.Empty;
        sb.Append(value.Length).Append(':').Append(value).Append(';');
    }
}