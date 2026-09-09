using System.Windows;
using System.Windows.Media;
using MiniCds.Domain.Abstractions;

namespace MiniCds.Wpf.Views;

public partial class AuditWindow : Window
{
    private readonly IAuditTrail _auditTrail;

    public AuditWindow(IAuditTrail auditTrail)
    {
        InitializeComponent();
        _auditTrail = auditTrail;
        Loaded += async (_, _) => await LoadAuditEntriesAsync();
    }

    private async Task LoadAuditEntriesAsync()
    {
        try
        {
            var entries = await _auditTrail.QueryAsync();
            AuditGrid.ItemsSource = entries;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load audit entries: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await LoadAuditEntriesAsync();
    }

    private async void OnVerifyClick(object sender, RoutedEventArgs e)
    {
        VerifyButton.IsEnabled = false;
        VerifyResultText.Text = "Verifying...";
        VerifyResultText.Foreground = Brushes.Gray;

        try
        {
            bool isIntact = await _auditTrail.VerifyChainAsync();
            if (isIntact)
            {
                VerifyResultText.Text = "Chain integrity: INTACT";
                VerifyResultText.Foreground = Brushes.Green;
            }
            else
            {
                VerifyResultText.Text = "Chain integrity: TAMPERED!";
                VerifyResultText.Foreground = Brushes.Red;
                MessageBox.Show("Hash chain verification failed! Audit trail has been tampered with.",
                    "Integrity Check Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            VerifyResultText.Text = "Verification failed";
            VerifyResultText.Foreground = Brushes.Red;
            MessageBox.Show($"Verification error: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            VerifyButton.IsEnabled = true;
        }
    }
}
