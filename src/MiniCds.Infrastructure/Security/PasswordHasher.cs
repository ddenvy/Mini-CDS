// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Security\PasswordHasher.cs
using System.Security.Cryptography;
using MiniCds.Domain.Abstractions;

namespace MiniCds.Infrastructure.Security;

/// <summary>
/// PBKDF2-SHA256 password hashing (100k iterations, 16-byte random salt, 32-byte key).
/// Adequate for this portfolio demo; production note in the plan calls for Argon2id.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int Iterations = 100_000;

    /// <inheritdoc />
    public (string hash, string salt) Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] hash = Derive(password, salt);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    /// <inheritdoc />
    public bool Verify(string password, string hash, string salt)
    {
        // Corrupted stored data must return false, not throw — login flow stays simple.
        // TryFromBase64String decodes into a fixed-size destination buffer, so any
        // payload longer than expected fails with bytesWritten mismatch or false.
        byte[] expected = new byte[HashSizeBytes];
        if (!Convert.TryFromBase64String(hash, expected, out int hashWritten) || hashWritten != HashSizeBytes)
            return false;

        byte[] saltBytes = new byte[SaltSizeBytes];
        if (!Convert.TryFromBase64String(salt, saltBytes, out int saltWritten) || saltWritten != SaltSizeBytes)
            return false;

        byte[] actual = Derive(password, saltBytes);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
}