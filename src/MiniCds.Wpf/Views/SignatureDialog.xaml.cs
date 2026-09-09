using System.ComponentModel;
using System.Windows;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Wpf.ViewModels;

namespace MiniCds.Wpf.Views;

public partial class SignatureDialog : Window
{
    private readonly SignatureDialogViewModel _viewModel;

    public SignatureDialog(ISignatureService signatureService, long actorUserId)
    {
        InitializeComponent();

        ElectronicSignature? captured = null;
        _viewModel = new SignatureDialogViewModel(
            signatureService,
            actorUserId,
            result =>
            {
                DialogResult = result;
                Close();
            },
            signature => captured = signature);

        DataContext = _viewModel;
        Closed += (_, _) => Signature = captured;

        PasswordBox.PasswordChanged += (_, _) => _viewModel.Password = PasswordBox.Password;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.ErrorMessage))
        {
            ErrorTextBlock.Visibility = string.IsNullOrEmpty(_viewModel.ErrorMessage)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    public ElectronicSignature? Signature { get; private set; }

    public void SetEntity(string entityType, long entityId)
    {
        _viewModel.LinkedEntityType = entityType;
        _viewModel.LinkedEntityId = entityId;
    }
}
