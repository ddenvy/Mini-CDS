using System.Windows;
using MiniCds.Domain.Abstractions;
using MiniCds.Wpf.ViewModels;

namespace MiniCds.Wpf.Views;

public partial class ReportDialog : Window
{
    private readonly ReportDialogViewModel _viewModel;

    public ReportDialog(ISampleRepository sampleRepository)
    {
        InitializeComponent();
        _viewModel = new ReportDialogViewModel(sampleRepository, result =>
        {
            DialogResult = result;
            Close();
        });
        DataContext = _viewModel;

        Loaded += async (s, e) => await _viewModel.InitializeAsync();
    }

    public IReadOnlyList<long> GetSelectedSampleIds() => _viewModel.GetSelectedSampleIds();
    public string ReportTitle => _viewModel.ReportTitle;
    public string Format => _viewModel.SelectedFormat;
}
