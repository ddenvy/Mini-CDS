// c:\Develop\Mini-CDS\src\MiniCds.Wpf\ViewModels\ReportDialogViewModel.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Wpf.Infrastructure;

namespace MiniCds.Wpf.ViewModels;

public sealed class ReportDialogViewModel : INotifyPropertyChanged
{
    private readonly ISampleRepository _sampleRepository;
    private readonly Action<bool> _closeDialog;
    private string _reportTitle = "Chromatography Report";
    private bool _isLoading;

    public ReportDialogViewModel(ISampleRepository sampleRepository, Action<bool> closeDialog)
    {
        _sampleRepository = sampleRepository;
        _closeDialog = closeDialog;
        OkCommand = new RelayCommand(ExecuteOk, () => !IsLoading && AvailableSamples.Any(s => s.IsSelected));
        CancelCommand = new RelayCommand(() => _closeDialog(false));
    }

    public ObservableCollection<SampleSelectionItem> AvailableSamples { get; } = new();

    public string ReportTitle
    {
        get => _reportTitle;
        set
        {
            if (_reportTitle == value) return;
            _reportTitle = value;
            OnPropertyChanged();
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
        }
    }

    public ICommand OkCommand { get; }
    public ICommand CancelCommand { get; }

    public IReadOnlyList<long> GetSelectedSampleIds()
    {
        return AvailableSamples
            .Where(s => s.IsSelected)
            .Select(s => s.Sample.Id)
            .ToList();
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            var samples = await _sampleRepository.GetAllAsync();
            AvailableSamples.Clear();
            foreach (var sample in samples)
            {
                AvailableSamples.Add(new SampleSelectionItem(sample));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ExecuteOk()
    {
        _closeDialog(true);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class SampleSelectionItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public SampleSelectionItem(Sample sample)
    {
        Sample = sample;
    }

    public Sample Sample { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public string DisplayName => $"{Sample.Name} ({Sample.Status})";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}