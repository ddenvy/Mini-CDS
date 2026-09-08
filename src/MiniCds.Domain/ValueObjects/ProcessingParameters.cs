namespace MiniCds.Domain.ValueObjects;

/// <summary>
/// DSP configuration parameters. Drives filtering and peak detection.
/// Stored in Method entity, passed down the DSP pipeline.
/// </summary>
public readonly record struct ProcessingParameters
{
    public int SampleRateHz { get; init; }
    public int MovingAverageWindow { get; init; }
    public int SavitzkyGolayWindow { get; init; }
    public int SavitzkyGolayOrder { get; init; }
    public double BaselineLambda { get; init; }
    public double BaselineP { get; init; }
    public double MinPeakHeight { get; init; }
    public double MinProminence { get; init; }
    public int MinWidthPoints { get; init; }
}