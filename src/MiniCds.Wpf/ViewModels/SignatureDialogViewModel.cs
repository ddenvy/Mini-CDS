using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;
using MiniCds.Wpf.Infrastructure;

namespace MiniCds.Wpf.ViewModels;

public sealed class SignatureDialogViewModel : INotifyPropertyChanged
{
    private readonly ISignatureService _signatureService;
    private readonly long _actorUserId;
    private readonly Action<bool> _closeDialog;
    private readonly Action<ElectronicSignature>? _onSigned;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private SignatureMeaning _selectedMeaning = SignatureMeaning.Approved;
    private string _reason = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public SignatureDialogViewModel(
        ISignatureService signatureService,
        long actorUserId,
        Action<bool> closeDialog,
        Action<ElectronicSignature>? onSigned = null)
    {
        _signatureService = signatureService;
        _actorUserId = actorUserId;
        _closeDialog = closeDialog;
        _onSigned = onSigned;

        SignCommand = new AsyncRelayCommand(SignAsync, CanSign);
        CancelCommand = new RelayCommand(() => _closeDialog(false));
    }

    public string Username
    {
        get => _username;
        set
        {
            if (_username == value) return;
            _username = value;
            OnPropertyChanged();
            RaiseCommands();
        }
    }

    public string Password
    {
        set
        {
            if (_password == value) return;
            _password = value;
            RaiseCommands();
        }
    }

    public SignatureMeaning SelectedMeaning
    {
        get => _selectedMeaning;
        set
        {
            if (_selectedMeaning == value) return;
            _selectedMeaning = value;
            OnPropertyChanged();
        }
    }

    public string Reason
    {
        get => _reason;
        set
        {
            if (_reason == value) return;
            _reason = value;
            OnPropertyChanged();
            RaiseCommands();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            RaiseCommands();
        }
    }

    public IReadOnlyList<SignatureMeaning> AvailableMeanings { get; } =
        Enum.GetValues<SignatureMeaning>();

    public ICommand SignCommand { get; }
    public ICommand CancelCommand { get; }

    private bool CanSign() =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(_password) &&
        !string.IsNullOrWhiteSpace(Reason);

    public async Task SignAsync()
    {
        if (!CanSign()) return;

        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            var signature = await _signatureService.SignAsync(
                Username,
                _password,
                SelectedMeaning,
                Reason,
                LinkedEntityType,
                LinkedEntityId,
                _actorUserId);

            if (signature is null)
            {
                ErrorMessage = "Invalid username or password.";
                return;
            }

            _onSigned?.Invoke(signature);
            _closeDialog(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Signing failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public string LinkedEntityType { get; set; } = string.Empty;
    public long LinkedEntityId { get; set; }

    private void RaiseCommands()
    {
        ((AsyncRelayCommand)SignCommand).RaiseCanExecuteChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
