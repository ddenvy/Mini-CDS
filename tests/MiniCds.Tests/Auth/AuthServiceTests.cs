// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Auth\AuthServiceTests.cs
using FluentAssertions;
using MiniCds.Application.Auth;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;
using MiniCds.Infrastructure.Security;
using NSubstitute;

namespace MiniCds.Tests.Auth;

public class AuthServiceTests
{
    private readonly IUserStore _userStore = Substitute.For<IUserStore>();
    private readonly IAuditTrail _auditTrail = Substitute.For<IAuditTrail>();
    private readonly PasswordHasher _hasher = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _userStore.GetSystemUserIdAsync(Arg.Any<CancellationToken>()).Returns(99);
        _auditTrail.AppendAsync(Arg.Any<AuditAction>(), Arg.Any<string>(), Arg.Any<long>(),
            Arg.Any<string?>(), Arg.Any<object?>(), Arg.Any<object?>(), Arg.Any<long>(), Arg.Any<long?>())
            .Returns(1);
        _service = new AuthService(_userStore, _hasher, _auditTrail);
    }

    private User MakeUser(string username = "alice", bool active = true, string password = "Good!Pass1")
    {
        var (hash, salt) = _hasher.Hash(password);
        return new User
        {
            Id = 7,
            Username = username,
            FullName = "Alice Tester",
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRole.Analyst,
            IsActive = active,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task Login_CorrectCredentials_Succeeds_AndAuditsAsSystem()
    {
        _userStore.FindByUsernameAsync("alice", Arg.Any<CancellationToken>()).Returns(MakeUser());

        var result = await _service.LoginAsync("alice", "Good!Pass1");

        result.Succeeded.Should().BeTrue();
        result.UserId.Should().Be(7);
        result.Role.Should().Be(UserRole.Analyst);
        await _auditTrail.Received(1).AppendAsync(
            AuditAction.Login, "User", 7, Arg.Any<string?>(), Arg.Any<object?>(), Arg.Any<object>(), 99, Arg.Any<long?>());
    }

    [Fact]
    public async Task Login_UnknownUser_Fails_AndAuditsFailedLogin()
    {
        _userStore.FindByUsernameAsync("ghost", Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _service.LoginAsync("ghost", "whatever");

        result.Succeeded.Should().BeFalse();
        await _auditTrail.Received(1).AppendAsync(
            AuditAction.FailedLogin, "User", 0,
            Arg.Is<string>(r => r!.Contains("unknown user") && r.Contains("ghost")),
            Arg.Any<object?>(), Arg.Any<object?>(), 99, Arg.Any<long?>());
    }

    [Fact]
    public async Task Login_WrongPassword_Fails_AndAuditsFailedLogin()
    {
        _userStore.FindByUsernameAsync("alice", Arg.Any<CancellationToken>()).Returns(MakeUser());

        var result = await _service.LoginAsync("alice", "Bad!Pass9");

        result.Succeeded.Should().BeFalse();
        await _auditTrail.Received(1).AppendAsync(
            AuditAction.FailedLogin, "User", 7,
            Arg.Is<string>(r => r!.Contains("wrong password")), Arg.Any<object?>(), Arg.Any<object?>(), 99, Arg.Any<long?>());
    }

    [Fact]
    public async Task Login_DisabledAccount_Fails()
    {
        _userStore.FindByUsernameAsync("alice", Arg.Any<CancellationToken>())
            .Returns(MakeUser(active: false));

        var result = await _service.LoginAsync("alice", "Good!Pass1");

        result.Succeeded.Should().BeFalse();
        await _auditTrail.Received(1).AppendAsync(
            AuditAction.FailedLogin, "User", 7,
            Arg.Is<string>(r => r!.Contains("account disabled")), Arg.Any<object?>(), Arg.Any<object?>(), 99, Arg.Any<long?>());
    }

    [Fact]
    public async Task Login_UnknownAndWrongPassword_ReturnIdenticalMessages()
    {
        _userStore.FindByUsernameAsync("ghost", Arg.Any<CancellationToken>()).Returns((User?)null);
        _userStore.FindByUsernameAsync("alice", Arg.Any<CancellationToken>()).Returns(MakeUser());

        var unknown = await _service.LoginAsync("ghost", "x");
        var wrongPassword = await _service.LoginAsync("alice", "x");

        unknown.Error.Should().Be(wrongPassword.Error);
    }

    [Fact]
    public async Task Login_WhenAuditWriteFails_AccessIsDenied()
    {
        _userStore.FindByUsernameAsync("alice", Arg.Any<CancellationToken>()).Returns(MakeUser());
        _auditTrail.AppendAsync(
                AuditAction.Login, Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string?>(),
                Arg.Any<object?>(), Arg.Any<object?>(), Arg.Any<long>(), Arg.Any<long?>())
            .Returns(Task.FromException<long>(new InvalidOperationException("journal unavailable")));

        var act = () => _service.LoginAsync("alice", "Good!Pass1");

        // Fail closed: no audit -> no access.
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
