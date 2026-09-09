// c:\Develop\Mini-CDS\src\MiniCds.Wpf\ViewModels\LoginViewModel.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MiniCds.Application.Auth;
using MiniCds.Wpf.Infrastructure;

namespace MiniCds.Wpf.ViewModels;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private readonly IAuthService _authService;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _isLoading;
    private string? _errorMessage;
    private AuthResult? _loginResult;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
        LoginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
    }

    public string Username
    {
        get => _username;
        set
        {
            if (_username == value) return;
            _username = value;
            OnPropertyChanged();
            ((AsyncRelayCommand)LoginCommand).RaiseCanExecuteChanged();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (_password == value) return;
            _password = value;
            OnPropertyChanged();
            ((AsyncRelayCommand)LoginCommand).RaiseCanExecuteChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged();
            ((AsyncRelayCommand)LoginCommand).RaiseCanExecuteChanged();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public AuthResult? LoginResult
    {
        get => _loginResult;
        private set
        {
            if (_loginResult == value) return;
            _loginResult = value;
            OnPropertyChanged();
        }
    }

    public ICommand LoginCommand { get; }

    private bool CanLogin() => !IsLoading && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);

    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            var result = await _authService.LoginAsync(Username, Password);
            LoginResult = result;
            if (!result.Succeeded)
            {
                ErrorMessage = result.Error ?? "Login failed.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}