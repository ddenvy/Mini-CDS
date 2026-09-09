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

        try
        {
            var report = await _reportService.GenerateReportAsync(selectedIds, title, format, _actorUserId);
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
}