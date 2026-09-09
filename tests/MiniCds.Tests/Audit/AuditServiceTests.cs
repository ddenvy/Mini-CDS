// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Audit\AuditServiceTests.cs
using FluentAssertions;
using MiniCds.Application.Audit;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Enums;
using NSubstitute;

namespace MiniCds.Tests.Audit;

/// <summary>
/// Unit tests for the Application-layer audit facade. The trail is mocked:
/// persistence is already covered by AuditTrailTests, here we verify WHAT gets requested.
/// </summary>
public class AuditServiceTests
{
    private readonly IAuditTrail _trail = Substitute.For<IAuditTrail>();
    private readonly AuditService _service;

    public AuditServiceTests()
    {
        _trail.AppendAsync(
                Arg.Any<AuditAction>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string?>(),
                Arg.Any<object?>(), Arg.Any<object?>(), Arg.Any<long>(), Arg.Any<long?>())
            .Returns(1);

        _service = new AuditService(_trail);
    }

    [Fact]
    public async Task RecordAsync_ForwardsAllFieldsToTrail()
    {
        var oldParams = new { Lambda = 1e5 };
        var newParams = new { Lambda = 1e6 };

        long id = await _service.RecordAsync(
            AuditAction.ChangeMethod, "Method", 42, "tuning",
            oldParams, newParams, actorUserId: 7);

        id.Should().Be(1);
        await _trail.Received(1).AppendAsync(
            AuditAction.ChangeMethod, "Method", 42, "tuning",
            oldParams, newParams, 7, null);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RecordAsync_BlankEntityType_IsRejected(string entityType)
    {
        var act = () => _service.RecordAsync(
            AuditAction.Sign, entityType, 1, null, null, null, actorUserId: 7);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*entityType*");

        await _trail.DidNotReceiveWithAnyArgs().AppendAsync(
            default, default!, default, default, default, default, default, default);
    }

    [Fact]
    public async Task RecordAsync_NullActor_IsRejected()
    {
        var act = () => _service.RecordAsync(
            AuditAction.Login, "User", 1, null, null, null, actorUserId: 0);

        await act.Should().ThrowAsync<ArgumentException>();

        await _trail.DidNotReceiveWithAnyArgs().AppendAsync(
            default, default!, default, default, default, default, default, default);
    }

    [Fact]
    public async Task VerifyIntegrityAsync_WhenChainValid_ReturnsTrue()
    {
        _trail.VerifyChainAsync(Arg.Any<CancellationToken>()).Returns(true);

        (await _service.VerifyIntegrityAsync()).Should().BeTrue();
        await _trail.Received(1).VerifyChainAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyIntegrityAsync_WhenChainBroken_ReturnsFalse()
    {
        _trail.VerifyChainAsync(Arg.Any<CancellationToken>()).Returns(false);

        (await _service.VerifyIntegrityAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task QueryLogAsync_ForwardsFilters()
    {
        var rows = new[] { new AuditEntryQueryRow(1, DateTime.UtcNow, "alice",
            AuditAction.Login, "User", 7, null, null, null, null) };
        _trail.QueryAsync(Arg.Any<string?>(), Arg.Any<long?>(), Arg.Any<AuditAction?>(),
                          Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(rows);

        var result = await _service.QueryLogAsync(entityType: "User", action: AuditAction.Login);

        result.Should().BeSameAs(rows);
        await _trail.Received(1).QueryAsync("User", null, AuditAction.Login, null, null,
            Arg.Any<CancellationToken>());
    }
}