using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MiniCds.Application.Reporting;
using MiniCds.Domain.Abstractions;
using MiniCds.Wpf.ViewModels;
using MiniCds.Wpf.Views;

namespace MiniCds.Wpf;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReportService _reportService;
    private readonly long _actorUserId;

    public MainWindow(IServiceProvider serviceProvider, IReportService reportService, long actorUserId)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _reportService = reportService;
        _actorUserId = actorUserId;

        var viewModel = ActivatorUtilities.CreateInstance<LiveChartViewModel>(
            _serviceProvider, _actorUserId);
        LiveChartControl.DataContext = viewModel;
    }

    private async void OnExportReportClick(object sender, RoutedEventArgs e)
    {
        var dialog = _serviceProvider.GetRequiredService<ReportDialog>();
        dialog.Owner = this;
        var result = dialog.ShowDialog();

        if (result != true) return;

        var selectedIds = dialog.GetSelectedSampleIds();
        if (selectedIds.Count == 0)
        {
            MessageBox.Show("Please select at least one sample.", "No samples selected",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var title = dialog.ReportTitle;
        var format = dialog.Format;
        var extension = format.Equals("PDF", StringComparison.OrdinalIgnoreCase) ? "pdf" : "csv";

        // Let the user choose where to save the report
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Report",
            FileName = $"{title}.{extension}",
            DefaultExt = extension,
            Filter = $"{format} files (*.{extension})|*.{extension}|All files (*.*)|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (saveDialog.ShowDialog(this) != true) return;

        var outputDirectory = System.IO.Path.GetDirectoryName(saveDialog.FileName);

        try
        {
            var report = await _reportService.GenerateReportAsync(
                selectedIds, title, format, _actorUserId, outputDirectory);
            MessageBox.Show($"Report exported successfully:\n{report.FilePath}",
                "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export report:\n{ex.Message}",
                "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnAuditTrailClick(object sender, RoutedEventArgs e)
    {
        var window = _serviceProvider.GetRequiredService<AuditWindow>();
        window.Owner = this;
        window.Show();
    }
}