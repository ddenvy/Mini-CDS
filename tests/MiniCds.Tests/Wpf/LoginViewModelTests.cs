// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Wpf\LoginViewModelTests.cs
using FluentAssertions;
using MiniCds.Application.Auth;
using MiniCds.Domain.Enums;
using MiniCds.Wpf.Infrastructure;
using MiniCds.Wpf.ViewModels;
using NSubstitute;

namespace MiniCds.Tests.Wpf;

public class LoginViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly LoginViewModel _viewModel;

    public LoginViewModelTests()
    {
        _viewModel = new LoginViewModel(_authService);
    }

    [Fact]
    public void InitialState_IsLoadingFalse_NoError()
    {
        _viewModel.IsLoading.Should().BeFalse();
        _viewModel.ErrorMessage.Should().BeNull();
        _viewModel.LoginResult.Should().BeNull();
    }

    [Fact]
    public void LoginCommand_CanExecute_WhenUsernameAndPasswordProvided()
    {
        _viewModel.Username = "admin";
        _viewModel.Password = "pass";

        _viewModel.LoginCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void LoginCommand_CannotExecute_WhenUsernameEmpty()
    {
        _viewModel.Username = "";
        _viewModel.Password = "pass";

        _viewModel.LoginCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void LoginCommand_CannotExecute_WhenPasswordEmpty()
    {
        _viewModel.Username = "admin";
        _viewModel.Password = "";

        _viewModel.LoginCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_Success_SetsLoginResult_NoError()
    {
        var successResult = AuthResult.Success(new()
        {
            Id = 1,
            Username = "admin",
            FullName = "Admin User",
            Role = UserRole.Administrator,
            IsActive = true,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            CreatedAtUtc = DateTime.UtcNow
        });
        _authService.LoginAsync("admin", "pass").Returns(successResult);

        _viewModel.Username = "admin";
        _viewModel.Password = "pass";
        await ((AsyncRelayCommand)_viewModel.LoginCommand).ExecuteAsync(null);

        _viewModel.LoginResult.Should().NotBeNull();
        _viewModel.LoginResult!.Succeeded.Should().BeTrue();
        _viewModel.ErrorMessage.Should().BeNull();
        _viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_Failure_SetsErrorMessage()
    {
        var failureResult = AuthResult.Failure("Invalid username or password.");
        _authService.LoginAsync("admin", "wrong").Returns(failureResult);

        _viewModel.Username = "admin";
        _viewModel.Password = "wrong";
        await ((AsyncRelayCommand)_viewModel.LoginCommand).ExecuteAsync(null);

        _viewModel.LoginResult.Should().NotBeNull();
        _viewModel.LoginResult!.Succeeded.Should().BeFalse();
        _viewModel.ErrorMessage.Should().Be("Invalid username or password.");
        _viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_ExceptionThrown_SetsErrorMessage()
    {
        _authService.LoginAsync("admin", "pass")
            .Returns(Task.FromException<AuthResult>(new InvalidOperationException("Audit unavailable")));

        _viewModel.Username = "admin";
        _viewModel.Password = "pass";
        await ((AsyncRelayCommand)_viewModel.LoginCommand).ExecuteAsync(null);

        _viewModel.ErrorMessage.Should().Be("Audit unavailable");
        _viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoginCommand_CannotExecute_WhileLoading()
    {
        var tcs = new TaskCompletionSource<AuthResult>();
        _authService.LoginAsync("admin", "pass").Returns(tcs.Task);

        _viewModel.Username = "admin";
        _viewModel.Password = "pass";
        var executeTask = ((AsyncRelayCommand)_viewModel.LoginCommand).ExecuteAsync(null);

        _viewModel.IsLoading.Should().BeTrue();
        _viewModel.LoginCommand.CanExecute(null).Should().BeFalse();

        tcs.SetResult(AuthResult.Success(new()
        {
            Id = 1,
            Username = "admin",
            FullName = "Admin",
            Role = UserRole.Administrator,
            IsActive = true,
            PasswordHash = "h",
            PasswordSalt = "s",
            CreatedAtUtc = DateTime.UtcNow
        }));
        await executeTask;
    }

    [Fact]
    public void PropertyChanged_RaisesForUsername()
    {
        string? changedProperty = null;
        _viewModel.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

        _viewModel.Username = "newuser";

        changedProperty.Should().Be(nameof(_viewModel.Username));
    }

    [Fact]
    public void PropertyChanged_RaisesForPassword()
    {
        string? changedProperty = null;
        _viewModel.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

        _viewModel.Password = "newpass";

        changedProperty.Should().Be(nameof(_viewModel.Password));
    }
}