using FluentAssertions;
using MiniCds.Application.SignalProcessing;
using MiniCds.Domain.ValueObjects;

namespace MiniCds.Tests.SignalProcessing;

public class PeakDetectorTests
{
    private static readonly ProcessingParameters DefaultParams = new()
    {
        SampleRateHz = 100,
        MinPeakHeight = 0.5,
        MinProminence = 0.3,
        MinWidthPoints = 3
    };

    /// <summary>
    /// Test that the peak detector returns one peak for a single Gaussian signal.
    /// </summary>
    [Fact]
    public void SingleGaussian_ReturnsOnePeak()
    {
        var (times, values) = Helpers.TestSignalGenerator.GenerateSingleGaussian(
            sampleRateHz: 100.0,
            durationSeconds: 10.0,
            peakAmplitude: 1.0,
            retentionTimeSeconds: 5.0,
            sigmaSeconds: 0.5);

        // Pure signal — tests PeakDetector only, no baseline correction.
        // Baseline correction on a pure Gaussian (≈0 on edges) can collapse
        // the peak since there's no "baseline region" for ALS to anchor to.
        var peaks = PeakDetector.Detect(times, values, DefaultParams);

        peaks.Should().HaveCount(1);
        peaks[0].Metrics.RetentionTime.Should().BeApproximately(5.0, 0.1);
        peaks[0].Metrics.Height.Should().BeGreaterThan(0.5);
    }

    /// <summary>
    /// Detects two closely spaced Gaussians with different amplitudes.
    /// </summary>
    [Fact]
    public void TwoGaussians_CloseButSeparated()
    {
        var (times, values) = Helpers.TestSignalGenerator.GenerateMultiPeak(
            sampleRateHz: 100.0,
            durationSeconds: 15.0,
            peaks: new[]
            {
                new Helpers.PeakDefinition(Amplitude: 1.0, RetentionTime: 4.0, Sigma: 0.3),
                new Helpers.PeakDefinition(Amplitude: 0.8, RetentionTime: 10.0, Sigma: 0.5)
            });

        double[] smoothed = MovingAverageFilter.Apply(values, window: 11);
        double[] corrected = BaselineCorrector.Correct(smoothed, lambda: 1e5, p: 0.001);

        var peaks = PeakDetector.Detect(times, corrected, DefaultParams);

        peaks.Count.Should().BeGreaterThanOrEqualTo(2);
        peaks.Select(p => p.Metrics.RetentionTime)
             .Should().Contain(rt => Math.Abs(rt - 4.0) < 0.2);
        peaks.Select(p => p.Metrics.RetentionTime)
             .Should().Contain(rt => Math.Abs(rt - 10.0) < 0.3);
    }

    [Fact]
    public void NoiseOnly_WithHighThreshold_ReturnsZero()
    {
        var (times, values) = Helpers.TestSignalGenerator.GenerateNoiseOnly(
            sampleRateHz: 100.0,
            durationSeconds: 5.0,
            noiseStdDev: 0.1);

        var peaks = PeakDetector.Detect(times, values, DefaultParams);

        peaks.Should().BeEmpty();
    }

    [Fact]
    public void Gaussian_AreaInReasonableRangeAfterProcessing()
    {
        // After MA smoothing + ALS baseline correction, area will be lower
        // than analytical. We check it's STILL POSITIVE and a reasonable fraction.
        const double amplitude = 1.5;
        const double sigma = 0.4;
        var (times, values) = Helpers.TestSignalGenerator.GenerateSingleGaussian(
            sampleRateHz: 100.0,
            durationSeconds: 8.0,
            peakAmplitude: amplitude,
            retentionTimeSeconds: 4.0,
            sigmaSeconds: sigma);

        double[] smoothed = MovingAverageFilter.Apply(values, window: 11);
        double[] corrected = BaselineCorrector.Correct(smoothed, lambda: 1e5, p: 0.05);

        var peaks = PeakDetector.Detect(times, corrected, DefaultParams);

        peaks.Should().HaveCount(1);
        double analyticalArea = Helpers.TestSignalGenerator.GaussianArea(amplitude, sigma);

        // Area must be positive and at least ~10% of analytical after smoothing/baseline
        peaks[0].Metrics.Area.Should().BeGreaterThan(analyticalArea * 0.10);
        peaks[0].Metrics.Area.Should().BeLessThan(analyticalArea * 2.5);
    }
}