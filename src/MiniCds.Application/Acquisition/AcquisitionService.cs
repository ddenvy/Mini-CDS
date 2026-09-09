// c:\Develop\Mini-CDS\src\MiniCds.Application\Acquisition\AcquisitionService.cs
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;
using MiniCds.Domain.ValueObjects;

namespace MiniCds.Application.Acquisition;

/// <summary>
/// Orchestrates chromatographic acquisition: subscribes to IInstrumentSource,
/// buffers SignalFrame data, applies DSP pipeline, persists RawSignal + Peak entities,
/// manages Sample lifecycle (Queued → Running → Completed), and audits all actions.
/// </summary>
public sealed class AcquisitionService
{
    private readonly ISampleRepository _sampleRepository;
    private readonly IMethodRepository _methodRepository;
    private readonly IRawSignalRepository _rawSignalRepository;
    private readonly IPeakRepository _peakRepository;
    private readonly ISignalProcessor _signalProcessor;
    private readonly IAuditTrail _auditTrail;
    private readonly List<SignalFrame> _frameBuffer = new();
    private IInstrumentSource? _instrument;
    private Sample? _currentSample;
    private long _actorUserId;

    public AcquisitionService(
        ISampleRepository sampleRepository,
        IMethodRepository methodRepository,
        IRawSignalRepository rawSignalRepository,
        IPeakRepository peakRepository,
        ISignalProcessor signalProcessor,
        IAuditTrail auditTrail)
    {
        _sampleRepository = sampleRepository;
        _methodRepository = methodRepository;
        _rawSignalRepository = rawSignalRepository;
        _peakRepository = peakRepository;
        _signalProcessor = signalProcessor;
        _auditTrail = auditTrail;
    }

    /// <summary>
    /// Raised when a new SignalFrame is processed (for real-time UI updates).
    /// </summary>
    public event EventHandler<SignalFrame>? FrameProcessed;

    /// <summary>
    /// Raised when peaks are detected (for UI notification).
    /// </summary>
    public event EventHandler<IReadOnlyList<DetectedPeak>>? PeaksDetected;

    /// <summary>
    /// Current sample being acquired (null if not started).
    /// </summary>
    public Sample? CurrentSample => _currentSample;

    /// <summary>
    /// Starts acquisition for the specified sample.
    /// Subscribes to instrument events, buffers frames, and transitions Sample to Running.
    /// </summary>
    public async Task StartAsync(
        long sampleId,
        IInstrumentSource instrument,
        long actorUserId,
        CancellationToken ct = default)
    {
        if (_instrument is not null)
            throw new InvalidOperationException("Acquisition already in progress.");

        _currentSample = await _sampleRepository.FindByIdAsync(sampleId, ct)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (_currentSample.Status != SampleStatus.Queued)
            throw new InvalidOperationException(
                $"Sample {_currentSample.Name} is not in Queued state (current: {_currentSample.Status}).");

        _instrument = instrument;
        _actorUserId = actorUserId;
        _frameBuffer.Clear();

        _instrument.FrameReceived += OnFrameReceived;

        // Transition Sample to Running
        await _sampleRepository.UpdateStatusAsync(_currentSample.Id, SampleStatus.Running, ct);

        await _auditTrail.AppendAsync(
            AuditAction.SampleStatusChanged,
            nameof(Sample),
            _currentSample.Id,
            $"Status changed to {SampleStatus.Running}",
            oldValues: new { Status = SampleStatus.Queued.ToString() },
            newValues: new { Status = SampleStatus.Running.ToString() },
            actorUserId: _actorUserId);

        await _instrument.StartAsync(ct);
    }

    /// <summary>
    /// Stops acquisition, persists RawSignal + Peak entities, transitions Sample to Completed.
    /// </summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_instrument is null || _currentSample is null)
            throw new InvalidOperationException("No acquisition in progress.");

        await _instrument.StopAsync(ct);
        _instrument.FrameReceived -= OnFrameReceived;

        // Persist RawSignal
        var rawSignal = new RawSignal
        {
            SampleId = _currentSample.Id,
            CapturedAtUtc = DateTime.UtcNow,
            SampleRateHz = _frameBuffer.Count > 1
                ? (int)(1.0 / (_frameBuffer[1].TimestampSeconds - _frameBuffer[0].TimestampSeconds))
                : 0,
            Points = ConvertFramesToBytes(_frameBuffer)
        };
        await _rawSignalRepository.AddAsync(rawSignal, ct);

        // Apply DSP pipeline
        var times = _frameBuffer.Select(f => f.TimestampSeconds).ToArray();
        var values = _frameBuffer.Select(f => f.Value).ToArray();

        var method = await _methodRepository.FindByIdAsync(_currentSample.MethodId, ct)
            ?? throw new InvalidOperationException($"Method {_currentSample.MethodId} not found.");

        var processed = _signalProcessor.Process(times, values, method.Parameters);

        // Persist detected peaks
        var detectedPeaks = processed.Peaks.ToList();
        var peakEntities = new List<Peak>();
        foreach (var dp in detectedPeaks)
        {
            var peak = new Peak
            {
                SampleId = _currentSample.Id,
                ApexIndex = dp.ApexIndex,
                StartIndex = dp.StartIndex,
                EndIndex = dp.EndIndex,
                Metrics = dp.Metrics,
                DetectedAtUtc = DateTime.UtcNow,
                IsManual = false
            };
            peakEntities.Add(peak);
        }
        await _peakRepository.AddRangeAsync(peakEntities, ct);

        // Transition Sample to Completed
        await _sampleRepository.UpdateStatusAsync(_currentSample.Id, SampleStatus.Completed, ct);

        await _auditTrail.AppendAsync(
            AuditAction.SampleStatusChanged,
            nameof(Sample),
            _currentSample.Id,
            $"Status changed to {SampleStatus.Completed}",
            oldValues: new { Status = SampleStatus.Running.ToString() },
            newValues: new { Status = SampleStatus.Completed.ToString() },
            actorUserId: _actorUserId);

        if (detectedPeaks.Count > 0)
        {
            await _auditTrail.AppendAsync(
                AuditAction.PeakDetected,
                nameof(Sample),
                _currentSample.Id,
                $"Detected {detectedPeaks.Count} peaks",
                oldValues: null,
                newValues: new { PeakCount = detectedPeaks.Count },
                actorUserId: _actorUserId);
        }

        PeaksDetected?.Invoke(this, detectedPeaks);

        _instrument = null;
        _currentSample = null;
        _frameBuffer.Clear();
    }

    private void OnFrameReceived(object? sender, SignalFrame frame)
    {
        _frameBuffer.Add(frame);
        FrameProcessed?.Invoke(this, frame);
    }

    private static byte[] ConvertFramesToBytes(List<SignalFrame> frames)
    {
        var values = frames.Select(f => f.Value).ToArray();
        var bytes = new byte[values.Length * sizeof(double)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}