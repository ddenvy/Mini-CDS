using MiniCds.Domain.Abstractions;
using MiniCds.Domain.ValueObjects;

namespace MiniCds.Application.SignalProcessing;

/// <summary>
/// Default DSP pipeline: MA smoothing → Savitzky-Golay → ALS baseline → peak detection.
/// </summary>
public sealed class SignalProcessor : ISignalProcessor
{
    /// <inheritdoc />
    public ProcessedSignal Process(ReadOnlySpan<double> times, ReadOnlySpan<double> raw, ProcessingParameters parameters)
    {
        if (times.Length != raw.Length)
            throw new ArgumentException(
                $"times and raw must have the same length (got {times.Length} and {raw.Length}).",
                nameof(raw));

        double[] smoothed = MovingAverageFilter.Apply(raw, parameters.MovingAverageWindow);
        smoothed = SavitzkyGolayFilter.Apply(smoothed, parameters.SavitzkyGolayWindow, parameters.SavitzkyGolayOrder);

        double[] baseline = BaselineCorrector.FitBaseline(smoothed, parameters.BaselineLambda, parameters.BaselineP);

        double[] corrected = new double[smoothed.Length];
        for (int i = 0; i < smoothed.Length; i++)
            corrected[i] = smoothed[i] - baseline[i];

        List<DetectedPeak> peaks = PeakDetector.Detect(times, corrected, parameters);

        return new ProcessedSignal(smoothed, baseline, corrected, peaks);
    }
}