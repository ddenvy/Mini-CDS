using MiniCds.Domain.Enums;
using MiniCds.Domain.Entities;

namespace MiniCds.Domain.Abstractions;

/// <summary>
/// Handles 21 CFR Part 11 electronic signatures.
/// Requires re-authentication (username + password) before signing.
/// </summary>
public interface ISignatureService
{
    Task<ElectronicSignature?> SignAsync(
        string username,
        string password,
        SignatureMeaning meaning,
        string reason,
        string linkedEntityType,
        long linkedEntityId,
        long actorUserId);
}