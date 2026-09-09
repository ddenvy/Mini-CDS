// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Security\PasswordHasherTests.cs
using FluentAssertions;
using MiniCds.Infrastructure.Security;

namespace MiniCds.Tests.Security;

public class PasswordHasherTests
{
    private static readonly PasswordHasher Hasher = new();

    [Fact]
    public void Hash_ThenVerify_CorrectPassword_ReturnsTrue()
    {
        var (hash, salt) = Hasher.Hash("S3cret!Chrom");

        Hasher.Verify("S3cret!Chrom", hash, salt).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var (hash, salt) = Hasher.Hash("S3cret!Chrom");

        Hasher.Verify("s3cret!chrom", hash, salt).Should().BeFalse();
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentSaltAndHash()
    {
        var first = Hasher.Hash("repeat");
        var second = Hasher.Hash("repeat");

        first.salt.Should().NotBe(second.salt);
        first.hash.Should().NotBe(second.hash);

        // Both remain independently verifiable — salt is stored per user.
        Hasher.Verify("repeat", first.hash, first.salt).Should().BeTrue();
        Hasher.Verify("repeat", second.hash, second.salt).Should().BeTrue();
    }

    [Fact]
    public void Verify_CorruptedStoredHash_ReturnsFalseWithoutThrowing()
    {
        var (_, salt) = Hasher.Hash("any");

        var act = () => Hasher.Verify("any", "not-valid-base64!!", salt);

        act.Should().NotThrow().Which.Should().BeFalse();
    }

    [Fact]
    public void Hash_OutputIsBase64WithExpectedKeyAndSaltSizes()
    {
        var (hash, salt) = Hasher.Hash("format-check");

        Convert.FromBase64String(hash).Should().HaveCount(32);
        Convert.FromBase64String(salt).Should().HaveCount(16);
    }

    [Fact]
    public void Verify_UnicodePassword_RoundTrips()
    {
        // PBKDF2(string) encodes UTF-8 on both ends — non-ASCII must survive the round trip.
        var (hash, salt) = Hasher.Hash("Пароль🔬2026");

        Hasher.Verify("Пароль🔬2026", hash, salt).Should().BeTrue();
        Hasher.Verify("Пароль🔬2025", hash, salt).Should().BeFalse();
    }
}