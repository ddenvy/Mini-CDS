using FluentAssertions;

namespace MiniCds.Tests.Helpers;

public class TestSignalGeneratorTests
{
    [Fact]
    public void SingleGaussian_AmplitudeCorrectWithinTolerance()
    {
        // Given: perfect noiseless Gaussian
        const double sampleRate = 100.0;       // 10 Hz = 100 point/sec
        const double duration = 10.0;           // 10 sec 
        const double amplitude = 1.0;
        const double retention = 5.0;           // peak at the center of the signal
        const double sigma = 0.5;

        // When
        var (times, values) = TestSignalGenerator.GenerateSingleGaussian(
            sampleRate, duration, amplitude, retention, sigma);

        // Then: Find
        int maxIdx = Array.IndexOf(values, values.Max());
        values[maxIdx].Should().BeApproximately(amplitude, 1e-9);
        times[maxIdx].Should().BeApproximately(retention, 1e-6);
    }

    [Fact]
    public void SingleGaussian_AreaMatchesAnalyticalFormula()
    {
        const double sampleRate = 100.0;
        const double duration = 10.0;
        const double amplitude = 2.0;
        const double retention = 5.0;
        const double sigma = 0.4;

        var (times, values) = TestSignalGenerator.GenerateSingleGaussian(
            sampleRate, duration, amplitude, retention, sigma);

        // Area via trapezoidal rule over all samples
        double dt = 1.0 / sampleRate;
        double trapezoid = 0;
        for (int i = 0; i < values.Length - 1; i++)
            trapezoid += 0.5 * (values[i] + values[i + 1]) * dt;

        // Analytical: A * sigma * sqrt(2*pi)
        double expected = TestSignalGenerator.GaussianArea(amplitude, sigma);

        // Numerical integration over the window approximates analytical,
        // with small tolerance due to window truncation at edges
        trapezoid.Should().BeApproximately(expected, expected * 0.01); // 1% tolerance
    }

    [Fact]
    public void MultiPeak_TwoGaussiansBothPresent()
    {
        const double sampleRate = 100.0;
        const double duration = 15.0;
        var peaks = new[]
        {
            new PeakDefinition(Amplitude: 1.0, RetentionTime: 4.0, Sigma: 0.3),
            new PeakDefinition(Amplitude: 0.8, RetentionTime: 10.0, Sigma: 0.5)
        };

        var (_, values) = TestSignalGenerator.GenerateMultiPeak(
            sampleRate, duration, peaks);

        // Expect exactly 2 local maxima
        int localMaxima = 0;
        for (int i = 1; i < values.Length - 1; i++)
        {
            if (values[i] > values[i - 1] && values[i] > values[i + 1])
                localMaxima++;
        }

        localMaxima.Should().Be(2);
    }

    [Fact]
    public void NoiseOnly_NoSignalAboveThreshold()
    {
        const double sampleRate = 100.0;
        const double duration = 5.0;

        var (_, values) = TestSignalGenerator.GenerateNoiseOnly(
            sampleRate, duration, noiseStdDev: 0.1);

        // No Gaussian peaks — signal oscillates around zero
        values.Max().Should().BeLessThan(0.5);
        values.Min().Should().BeGreaterThan(-0.5);
    }
}