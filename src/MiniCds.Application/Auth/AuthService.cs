// c:\Develop\Mini-CDS\src\MiniCds.Application\Auth\AuthService.cs
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;

namespace MiniCds.Application.Auth;

/// <summary>Outcome of a login attempt. Carries no credentials — safe to pass to the UI layer.</summary>
public sealed record AuthResult(
    bool Succeeded,
    string? Error,
    long UserId,
    string Username,
    string FullName,
    UserRole Role)
{
    public static AuthResult Success(User user)
        => new(true, null, user.Id, user.Username, user.FullName, user.Role);

    public static AuthResult Failure(string error)
        => new(false, error, 0, string.Empty, string.Empty, UserRole.Operator);
}

/// <summary>Authenticates a user and writes Login/FailedLogin to the audit trail.</summary>
public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct = default);
}

/// <summary>
/// Authenticates a user and writes Login/FailedLogin to the audit trail as the system account.
/// Fail-closed: if the audit entry cannot be written, access is denied.
/// </summary>
public sealed class AuthService(IUserStore userStore, IPasswordHasher passwordHasher, IAuditTrail auditTrail) : IAuthService
{
    private const string GenericError = "Invalid username or password.";

    // Random secret hashed once per process: unknown-user attempts burn the same
    // PBKDF2 budget as real verifications, so response time cannot enumerate users.
    private readonly Lazy<(string hash, string salt)> _dummyCredentials =
        new(() => passwordHasher.Hash(Guid.NewGuid().ToString("N")));

    public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await userStore.FindByUsernameAsync(username, ct);

        if (user is null || !user.IsActive)
        {
            passwordHasher.Verify(password, _dummyCredentials.Value.hash, _dummyCredentials.Value.salt);
            await AuditFailureAsync(user, username,
                user is null ? "unknown user" : "account disabled", ct);
            return AuthResult.Failure(GenericError);
        }

        if (!passwordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            await AuditFailureAsync(user, username, "wrong password", ct);
            return AuthResult.Failure(GenericError);
        }

        long systemUserId = await userStore.GetSystemUserIdAsync(ct);
        await auditTrail.AppendAsync(
            AuditAction.Login, "User", user.Id, null,
            null, new { user.Username }, systemUserId);

        return AuthResult.Success(user);
    }

    private async Task AuditFailureAsync(User? user, string attemptedUsername, string cause, CancellationToken ct)
    {
        long systemUserId = await userStore.GetSystemUserIdAsync(ct);
        await auditTrail.AppendAsync(
            AuditAction.FailedLogin, "User", user?.Id ?? 0,
            $"{cause}: '{attemptedUsername}'",
            null, null, systemUserId);
    }
}
