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
    private string _selectedFormat = "CSV";
    private bool _isLoading;
    private int _selectedCount;

    public ReportDialogViewModel(ISampleRepository sampleRepository, Action<bool> closeDialog)
    {
        _sampleRepository = sampleRepository;
        _closeDialog = closeDialog;
        OkCommand = new RelayCommand(ExecuteOk, () => !IsLoading && SelectedCount > 0);
        CancelCommand = new RelayCommand(() => _closeDialog(false));
    }

    public ObservableCollection<SampleSelectionItem> AvailableSamples { get; } = new();

    public IReadOnlyList<string> AvailableFormats { get; } = new[] { "CSV", "PDF" };

    public int SelectedCount
    {
        get => _selectedCount;
        private set
        {
            if (_selectedCount == value) return;
            _selectedCount = value;
            OnPropertyChanged();
        }
    }

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

    public string SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (_selectedFormat == value) return;
            _selectedFormat = value;
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
            RaiseOkCanExecuteChanged();
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
                var item = new SampleSelectionItem(sample);
                item.PropertyChanged += OnSampleItemPropertyChanged;
                AvailableSamples.Add(item);
            }
            UpdateSelectedCount();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnSampleItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SampleSelectionItem.IsSelected))
        {
            UpdateSelectedCount();
            RaiseOkCanExecuteChanged();
        }
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = AvailableSamples.Count(s => s.IsSelected);
    }

    private void RaiseOkCanExecuteChanged()
    {
        if (OkCommand is RelayCommand relay)
        {
            relay.RaiseCanExecuteChanged();
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