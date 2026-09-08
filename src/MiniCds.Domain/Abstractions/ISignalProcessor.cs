using MiniCds.Domain.ValueObjects;

namespace MiniCds.Domain.Abstractions;

/// <summary>
/// Full DSP pipeline: smoothing → baseline correction → peak detection.
/// Pure function — no side effects, stateless.
/// </summary>
public interface ISignalProcessor
{
    ProcessedSignal Process(ReadOnlySpan<double> times, ReadOnlySpan<double> raw, ProcessingParameters parameters);
}

/// <summary>
/// Result of DSP pipeline execution.
/// </summary>
public readonly record struct ProcessedSignal(
    ReadOnlyMemory<double> Smoothed,
    ReadOnlyMemory<double> Baseline,
    ReadOnlyMemory<double> Corrected,
    IReadOnlyList<DetectedPeak> Peaks);

/// <summary>
/// Peak detected by DSP pipeline. Position indices refer to the input array.
/// </summary>
public readonly record struct DetectedPeak(
    int ApexIndex,
    int StartIndex,
    int EndIndex,
    PeakMetrics Metrics);