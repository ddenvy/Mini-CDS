// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Audit\HashChainTests.cs
using FluentAssertions;
using MiniCds.Application.Audit;

namespace MiniCds.Tests.Audit;

public class HashChainTests
{
    private static readonly HashChain Chain = new();

    private static string H(
        string id = "1",
        string reason = "test",
        string prevHash = HashChain.GenesisPrevHash)
        => Chain.ComputeHash(id, "2026-09-08T12:00:00Z", "Login", "7", "User", "7",
                              reason, null, null, prevHash);

    [Fact]
    public void ComputeHash_IsDeterministic()
    {
        H().Should().Be(H());
    }

    [Fact]
    public void ComputeHash_Returns64CharLowercaseHex()
    {
        var hash = H();

        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void ComputeHash_ChangesWhenAnySingleFieldChanges()
    {
        var baseline = H();

        H(id: "2").Should().NotBe(baseline);
        H(reason: "other").Should().NotBe(baseline);
        H(prevHash: "ffff").Should().NotBe(baseline);
    }

    /// <summary>
    /// Core tamper-evidence property: content must not be movable across a field boundary.
    /// With a plain '|' join these two inputs collapse to one payload and collide.
    /// </summary>
    [Theory]
    [InlineData("a|b", "")]
    [InlineData("a", "b")]
    [InlineData("", "a|b")]
    public void ComputeHash_FieldBoundaryInjection_ProducesDistinctHashes(string reason, string oldValues)
    {
        var hash = Chain.ComputeHash("1", "2026-09-08T12:00:00Z", "Login", "7", "User", "7",
                                     reason, oldValues, null, HashChain.GenesisPrevHash);
        _seen.Add(hash);

        _seen.Should().OnlyHaveUniqueItems(
            $"payload collision between (reason='{reason}', oldValues='{oldValues}') combinations");
    }

    private static readonly HashSet<string> _seen = new();

    [Fact]
    public void ComputeHash_TreatsNullAsEmptyConsistently()
    {
        var withNull = Chain.ComputeHash("1", "t", "Login", "7", "User", "7",
                                         null, null, null, HashChain.GenesisPrevHash);
        var withEmpty = Chain.ComputeHash("1", "t", "Login", "7", "User", "7",
                                          "", "", "", HashChain.GenesisPrevHash);

        withNull.Should().Be(withEmpty);
    }

    [Fact]
    public void ChainOfTwoEntries_EachDependsOnPreviousHash()
    {
        var first = H(id: "1");
        var secondSame = Chain.ComputeHash("2", "2026-09-08T12:00:01Z", "Login", "7", "User", "7",
                                           "r", null, null, first);
        var secondTampered = Chain.ComputeHash("2", "2026-09-08T12:00:01Z", "Login", "7", "User", "7",
                                               "r", null, null, "deadbeef");

        secondSame.Should().NotBe(secondTampered);
    }
}