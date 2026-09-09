// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\SignatureService.cs
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MiniCds.Application.Audit;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;

namespace MiniCds.Infrastructure.Persistence;

/// <summary>
/// 21 CFR Part 11 electronic signing: re-authentication (username + password) followed by
/// an atomic insert of the signature and its audit entry. Both ids are allocated
/// deterministically (max+1) in one transaction because each row references the other
/// and append-only triggers forbid a follow-up UPDATE.
/// </summary>
public sealed class SignatureService(
    CdsDbContext db,
    IPasswordHasher passwordHasher,
    IHashChain hashChain) : ISignatureService
{
    /// <inheritdoc />
    public async Task<ElectronicSignature?> SignAsync(
        string username,
        string password,
        SignatureMeaning meaning,
        string reason,
        string linkedEntityType,
        long linkedEntityId,
        long actorUserId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        // Re-authentication: the signer must exist, be active, and be the acting user.
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null || !user.IsActive || user.Id != actorUserId)
            return null;
        if (!passwordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            return null;

        // Deterministic id allocation for both tables (same pattern as AuditTrail).
        var lastAudit = await db.AuditEntries.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Select(a => new { a.Id, a.Hash })
            .FirstOrDefaultAsync();
        long auditId = (lastAudit?.Id ?? 0) + 1;
        string prevHash = lastAudit?.Hash ?? HashChain.GenesisPrevHash;

        // MAX(Id) over an empty table is SQL NULL, hence long? + null-coalescing.
        long lastSignatureId = await db.ElectronicSignatures.AsNoTracking()
            .Select(s => (long?)s.Id)
            .MaxAsync() ?? 0;
        long signatureId = lastSignatureId + 1;

        var timestampUtc = DateTime.UtcNow;
        string hash = hashChain.ComputeHash(
            auditId.ToString(CultureInfo.InvariantCulture),
            timestampUtc.ToString("O", CultureInfo.InvariantCulture),
            AuditAction.Sign.ToString(),
            actorUserId.ToString(CultureInfo.InvariantCulture),
            linkedEntityType,
            linkedEntityId.ToString(CultureInfo.InvariantCulture),
            reason, null, null, prevHash);

        var signature = new ElectronicSignature
        {
            Id = signatureId,
            UserId = user.Id,
            Meaning = meaning,
            Reason = reason,
            TimestampUtc = timestampUtc,
            LinkedEntityType = linkedEntityType,
            LinkedEntityId = linkedEntityId,
            AuditEntryId = auditId
        };

        var audit = new AuditEntry
        {
            Id = auditId,
            TimestampUtc = timestampUtc,
            ActorUserId = actorUserId,
            Action = AuditAction.Sign,
            EntityType = linkedEntityType,
            EntityId = linkedEntityId,
            Reason = reason,
            SignatureId = signatureId,
            PrevHash = prevHash,
            Hash = hash
        };

        db.AuditEntries.Add(audit);
        db.ElectronicSignatures.Add(signature);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return signature;
    }
}