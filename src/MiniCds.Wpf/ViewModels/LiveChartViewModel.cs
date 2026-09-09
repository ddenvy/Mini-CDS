// c:\Develop\Mini-CDS\src\MiniCds.Wpf\ViewModels\LiveChartViewModel.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;
using MiniCds.Domain.ValueObjects;
using MiniCds.Wpf.Infrastructure;
using MiniCds.Wpf.Views;

namespace MiniCds.Wpf.ViewModels;

public sealed class LiveChartViewModel : INotifyPropertyChanged
{
    private readonly IAcquisitionService _acquisitionService;
    private readonly ISampleRepository _sampleRepository;
    private readonly IMethodRepository _methodRepository;
    private readonly IInstrumentSource _instrumentSource;
    private readonly IServiceProvider _serviceProvider;
    private readonly long _actorUserId;

    private bool _isAcquiring;
    private string _currentSampleName = "No sample selected";
    private int _peakCount;
    private Sample? _selectedSample;

    public LiveChartViewModel(
        IAcquisitionService acquisitionService,
        ISampleRepository sampleRepository,
        IMethodRepository methodRepository,
        IInstrumentSource instrumentSource,
        IServiceProvider serviceProvider,
        long actorUserId)
    {
        _acquisitionService = acquisitionService;
        _sampleRepository = sampleRepository;
        _methodRepository = methodRepository;
        _instrumentSource = instrumentSource;
        _serviceProvider = serviceProvider;
        _actorUserId = actorUserId;

        StartAcquisitionCommand = new AsyncRelayCommand(StartAcquisitionAsync, CanStartAcquisition);
        StopAcquisitionCommand = new AsyncRelayCommand(StopAcquisitionAsync, CanStopAcquisition);
        LoadSamplesCommand = new AsyncRelayCommand(LoadSamplesAsync, () => true);
        VoidSampleCommand = new AsyncRelayCommand(VoidSampleAsync, CanVoidSample);
        CreateSampleCommand = new AsyncRelayCommand(CreateSampleAsync, () => true);

        _acquisitionService.FrameProcessed += OnFrameProcessed;
        _acquisitionService.PeaksDetected += OnPeaksDetected;
    }

    public ObservableCollection<SignalFrame> SignalData { get; } = new();
    public ObservableCollection<DetectedPeak> DetectedPeaks { get; } = new();
    public ObservableCollection<Sample> AvailableSamples { get; } = new();

    public bool IsAcquiring
    {
        get => _isAcquiring;
        private set
        {
            if (_isAcquiring == value) return;
            _isAcquiring = value;
            OnPropertyChanged();
            ((AsyncRelayCommand)StartAcquisitionCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)StopAcquisitionCommand).RaiseCanExecuteChanged();
        }
    }

    public string CurrentSampleName
    {
        get => _currentSampleName;
        private set
        {
            if (_currentSampleName == value) return;
            _currentSampleName = value;
            OnPropertyChanged();
        }
    }

    public int PeakCount
    {
        get => _peakCount;
        private set
        {
            if (_peakCount == value) return;
            _peakCount = value;
            OnPropertyChanged();
        }
    }

    public Sample? SelectedSample
    {
        get => _selectedSample;
        set
        {
            if (_selectedSample == value) return;
            _selectedSample = value;
            OnPropertyChanged();
            ((AsyncRelayCommand)StartAcquisitionCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)VoidSampleCommand).RaiseCanExecuteChanged();
        }
    }

    public ICommand StartAcquisitionCommand { get; }
    public ICommand StopAcquisitionCommand { get; }
    public ICommand LoadSamplesCommand { get; }
    public ICommand VoidSampleCommand { get; }
    public ICommand CreateSampleCommand { get; }

    private bool CanStartAcquisition() => !IsAcquiring && SelectedSample is not null;

    private bool CanStopAcquisition() => IsAcquiring;

    private bool CanVoidSample() =>
        !IsAcquiring && SelectedSample is not null && SelectedSample.Status != SampleStatus.Voided;

    private async Task StartAcquisitionAsync()
    {
        if (SelectedSample is null) return;

        try
        {
            SignalData.Clear();
            DetectedPeaks.Clear();
            PeakCount = 0;
            IsAcquiring = true;
            CurrentSampleName = SelectedSample.Name;

            await _acquisitionService.StartAsync(
                SelectedSample.Id,
                _instrumentSource,
                _actorUserId);
        }
        catch (Exception ex)
        {
            IsAcquiring = false;
            MessageBox.Show($"Failed to start acquisition: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task StopAcquisitionAsync()
    {
        try
        {
            await _acquisitionService.StopAsync();
            IsAcquiring = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to stop acquisition: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task VoidSampleAsync()
    {
        if (SelectedSample is null) return;

        var dialog = ActivatorUtilities.CreateInstance<SignatureDialog>(_serviceProvider, _actorUserId);
        dialog.SetEntity(nameof(Sample), SelectedSample.Id);
        var result = dialog.ShowDialog();

        if (result != true || dialog.Signature is null) return;

        try
        {
            await _sampleRepository.UpdateStatusAsync(SelectedSample.Id, SampleStatus.Voided);
            SelectedSample.Status = SampleStatus.Voided;
            MessageBox.Show($"Sample '{SelectedSample.Name}' voided successfully.",
                "Sample Voided", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to void sample: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadSamplesAsync()
    {
        var samples = await _sampleRepository.GetAllAsync();
        AvailableSamples.Clear();
        foreach (var sample in samples)
        {
            AvailableSamples.Add(sample);
        }
    }

    private async Task CreateSampleAsync()
    {
        try
        {
            var methods = await _methodRepository.GetAllAsync();
            if (methods.Count == 0)
            {
                MessageBox.Show("No method available to create a sample.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var name = $"Sample_{DateTime.Now:yyyyMMddHHmmss}";
            var sample = new Sample
            {
                Name = name,
                MethodId = methods[0].Id,
                Status = SampleStatus.Queued,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = _actorUserId
            };

            await _sampleRepository.AddAsync(sample);
            await LoadSamplesAsync();

            // Select the newly created sample
            SelectedSample = AvailableSamples.FirstOrDefault(s => s.Name == name);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create sample: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnFrameProcessed(object? sender, SignalFrame frame)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            SignalData.Add(frame);
        });
    }

    private void OnPeaksDetected(object? sender, IReadOnlyList<DetectedPeak> peaks)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            DetectedPeaks.Clear();
            foreach (var peak in peaks)
            {
                DetectedPeaks.Add(peak);
            }
            PeakCount = DetectedPeaks.Count;
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}