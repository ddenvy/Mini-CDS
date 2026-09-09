// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Instruments\SimulatorInstrumentSource.cs
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Enums;
using MiniCds.Domain.ValueObjects;

namespace MiniCds.Infrastructure.Instruments;

/// <summary>
/// Simulates a chromatographic instrument by generating multi-Gaussian peaks in real time.
/// Raises FrameReceived event for each generated signal frame.
/// </summary>
public sealed class SimulatorInstrumentSource : IInstrumentSource
{
    private readonly int _sampleRateHz;
    private readonly double _durationSeconds;
    private readonly IReadOnlyList<SimulatorPeakDefinition> _peaks;
    private readonly double _noiseStdDev;
    private readonly double _baselineSlope;
    private CancellationTokenSource? _cts;
    private Task? _generationTask;

    public SimulatorInstrumentSource(
        int sampleRateHz,
        double durationSeconds,
        IReadOnlyList<SimulatorPeakDefinition> peaks,
        double noiseStdDev = 0,
        double baselineSlope = 0)
    {
        _sampleRateHz = sampleRateHz;
        _durationSeconds = durationSeconds;
        _peaks = peaks;
        _noiseStdDev = noiseStdDev;
        _baselineSlope = baselineSlope;
    }

    public InstrumentState State { get; private set; } = InstrumentState.Idle;

    public event EventHandler<SignalFrame>? FrameReceived;

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_generationTask is not null)
            throw new InvalidOperationException("Simulator already started.");

        State = InstrumentState.Streaming;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _generationTask = GenerateSignalAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_cts is null) return;

        State = InstrumentState.Stopping;
        await _cts.CancelAsync();
        if (_generationTask is not null)
        {
            try
            {
                await _generationTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
        State = InstrumentState.Idle;
    }

    public async ValueTask DisposeAsync()
    {
        if (State == InstrumentState.Streaming)
        {
            await StopAsync();
        }
        _cts?.Dispose();
    }

    private async Task GenerateSignalAsync(CancellationToken cancellationToken)
    {
        var rng = new Random(42);
        int totalPoints = (int)(_sampleRateHz * _durationSeconds);
        double dt = 1.0 / _sampleRateHz;

        try
        {
            for (int i = 0; i < totalPoints; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double t = i * dt;
                double value = 0;

                // Sum of Gaussian peaks
                foreach (var peak in _peaks)
                {
                    double exponent = -(t - peak.RetentionTime) * (t - peak.RetentionTime) 
                                     / (2.0 * peak.Sigma * peak.Sigma);
                    value += peak.Amplitude * Math.Exp(exponent);
                }

                // Baseline
                value += _baselineSlope * t;

                // Noise
                if (_noiseStdDev > 0)
                {
                    value += NextGaussian(rng) * _noiseStdDev;
                }

                var frame = new SignalFrame(t, value);
                FrameReceived?.Invoke(this, frame);

                // Simulate real-time acquisition delay
                await Task.Delay(TimeSpan.FromSeconds(dt), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private static double NextGaussian(Random rng)
    {
        // Box-Muller transform
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}

/// <summary>Definition of a Gaussian peak for simulator configuration.</summary>
public readonly record struct SimulatorPeakDefinition(
    double Amplitude,
    double RetentionTime,
    double Sigma);