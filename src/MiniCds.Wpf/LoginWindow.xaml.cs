// c:\Develop\Mini-CDS\src\MiniCds.Wpf\LoginWindow.xaml.cs
using System.Windows;
using System.Windows.Controls;
using MiniCds.Application.Auth;
using MiniCds.Wpf.ViewModels;

namespace MiniCds.Wpf;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(IAuthService authService)
    {
        InitializeComponent();
        _viewModel = new LoginViewModel(authService);
        DataContext = _viewModel;

        PasswordBox.PasswordChanged += (_, _) => _viewModel.Password = PasswordBox.Password;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.LoginResult) && _viewModel.LoginResult?.Succeeded == true)
            {
                DialogResult = true;
                Close();
            }
        };

        UsernameBox.Focus();
    }

    public AuthResult? AuthResult => _viewModel.LoginResult;
}