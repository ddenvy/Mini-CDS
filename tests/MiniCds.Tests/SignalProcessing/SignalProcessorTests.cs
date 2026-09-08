using FluentAssertions;
using MiniCds.Application.SignalProcessing;
using MiniCds.Domain.ValueObjects;
using MiniCds.Tests.Helpers;

namespace MiniCds.Tests.SignalProcessing;

/// <summary>
/// Integration tests for the full DSP pipeline (smoothing → baseline → peaks).
/// Unlike component tests, these verify the SYSTEM on realistic chromatograms.
/// </summary>
public class SignalProcessorTests
{
    private static readonly ProcessingParameters DefaultParams = new()
    {
        SampleRateHz = 100,
        MovingAverageWindow = 5,
        SavitzkyGolayWindow = 11,
        SavitzkyGolayOrder = 3,
        BaselineLambda = 1e5,
        BaselineP = 0.01,
        MinPeakHeight = 0.5,
        MinProminence = 0.3,
        MinWidthPoints = 3
    };

    /// <summary>
    /// Realistic chromatogram: drifting baseline + noise + 3 well-separated Gaussians.
    /// Pipeline must recover exactly 3 peaks at the true retention times.
    /// </summary>
    [Fact]
    public void ChromatogramWithThreePeaks_FindsAllAtCorrectRetentionTimes()
    {
        // Given
        var trueRetentionTimes = new[] { 5.0, 10.0, 15.0 };
        var (times, raw) = TestSignalGenerator.GenerateMultiPeak(
            sampleRateHz: 100.0,
            durationSeconds: 20.0,
            peaks: new[]
            {
                new PeakDefinition(Amplitude: 2.0, RetentionTime: 5.0, Sigma: 0.5),
                new PeakDefinition(Amplitude: 3.0, RetentionTime: 10.0, Sigma: 0.5),
                new PeakDefinition(Amplitude: 1.5, RetentionTime: 15.0, Sigma: 0.5)
            },
            noiseStdDev: 0.01,
            baselineSlope: 0.1);

        var processor = new SignalProcessor();

        // When
        var result = processor.Process(times, raw, DefaultParams);

        // Then
        result.Peaks.Should().HaveCount(3);
        for (int i = 0; i < 3; i++)
        {
            result.Peaks[i].Metrics.RetentionTime
                .Should().BeApproximately(trueRetentionTimes[i], 0.15);
        }

        // Heights ordered like amplitudes (3rd peak smallest, but above threshold)
        result.Peaks[0].Metrics.Height.Should().BeGreaterThan(DefaultParams.MinPeakHeight);
        result.Peaks[1].Metrics.Height.Should().BeGreaterThan(result.Peaks[2].Metrics.Height);

        // Pipeline artifacts returned with full length
        result.Smoothed.Length.Should().Be(raw.Length);
        result.Baseline.Length.Should().Be(raw.Length);
        result.Corrected.Length.Should().Be(raw.Length);
    }

    /// <summary>
    /// Noise-only signal must produce zero peaks:
    /// smoothing + height/prominence thresholds must suppress all noise bumps.
    /// </summary>
    [Fact]
    public void NoiseOnly_FindsNoPeaks()
    {
        // Given
        var (times, raw) = TestSignalGenerator.GenerateNoiseOnly(
            sampleRateHz: 100.0,
            durationSeconds: 10.0,
            noiseStdDev: 0.05);

        var processor = new SignalProcessor();

        // When
        var result = processor.Process(times, raw, DefaultParams);

        // Then
        result.Peaks.Should().BeEmpty();
    }

    /// <summary>
    /// ISignalProcessor contract: stateless pure function.
    /// Two runs on identical input must produce identical output.
    /// </summary>
    [Fact]
    public void Process_IsStateless_ReturnsIdenticalResultsOnSecondCall()
    {
        // Given
        var (times, raw) = TestSignalGenerator.GenerateSingleGaussian(
            sampleRateHz: 100.0,
            durationSeconds: 10.0,
            peakAmplitude: 2.0,
            retentionTimeSeconds: 5.0,
            sigmaSeconds: 0.5,
            noiseStdDev: 0.01,
            baselineSlope: 0.1);

        var processor = new SignalProcessor();

        // When
        var first = processor.Process(times, raw, DefaultParams);
        var second = processor.Process(times, raw, DefaultParams);

        // Then
        second.Peaks.Should().HaveCount(first.Peaks.Count);
        for (int i = 0; i < first.Peaks.Count; i++)
        {
            second.Peaks[i].ApexIndex.Should().Be(first.Peaks[i].ApexIndex);
            second.Peaks[i].Metrics.Area.Should().Be(first.Peaks[i].Metrics.Area);
        }
        second.Corrected.ToArray().Should().Equal(first.Corrected.ToArray());
    }

    /// <summary>
    /// Mismatched time/value arrays are a caller bug — must fail fast, not produce garbage.
    /// </summary>
    [Fact]
    public void Process_LengthMismatch_ThrowsArgumentException()
    {
        var times = new double[100];
        var raw = new double[99];
        var processor = new SignalProcessor();

        var act = () => processor.Process(times, raw, DefaultParams);

        act.Should().Throw<ArgumentException>();
    }
}