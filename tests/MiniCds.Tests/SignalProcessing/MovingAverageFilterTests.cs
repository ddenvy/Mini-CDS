using FluentAssertions;
using MiniCds.Application.SignalProcessing;

namespace MiniCds.Tests.SignalProcessing;

public class MovingAverageFilterTests
{
    [Fact]
    public void ConstantSignal_PassesUnchanged()
    {
        // Given: all values are exactly 5.0
        double[] input = Enumerable.Repeat(5.0, 100).ToArray();

        // When
        double[] output = MovingAverageFilter.Apply(input, window: 5);

        // Then: mean of [5,5,5,5,5] is still 5.0 everywhere
        output.Should().AllBeEquivalentTo(5.0);
        output.Length.Should().Be(input.Length);
    }

    [Fact]
    public void KnownShortSequence_MatchesExpectedMeans()
    {
        // Given: simple deterministic sequence
        double[] input = { 0, 0, 10, 0, 0 };
        int window = 3; // m=1, so at i=2 we average [0,10,0]=3.333

        // When
        double[] output = MovingAverageFilter.Apply(input, window);

        // Then
        // i=0: window=[0,0] => 0.0
        // i=1: window=[0,0,10] => 3.333
        // i=2: window=[0,10,0] => 3.333
        // i=3: window=[10,0,0] => 3.333
        // i=4: window=[0,0] => 0.0
        output[0].Should().BeApproximately(0.0, 1e-9);
        output[1].Should().BeApproximately(10.0 / 3.0, 1e-9);
        output[2].Should().BeApproximately(10.0 / 3.0, 1e-9);
        output[3].Should().BeApproximately(10.0 / 3.0, 1e-9);
        output[4].Should().BeApproximately(0.0, 1e-9);
    }

    [Fact]
    public void WindowTooLarge_WorksAtEdges()
    {
        double[] input = { 1, 2, 3, 4, 5 };
        // window=11 > input length — filter should still run,
        // each position uses ALL available samples
        double[] output = MovingAverageFilter.Apply(input, window: 11);

        // At i=2 (center): mean([1,2,3,4,5]) = 3.0
        output[2].Should().BeApproximately(3.0, 1e-9);
    }

    [Fact]
    public void EvenWindow_ThrowsArgumentOutOfRange()
    {
        double[] input = { 1, 2, 3, 4, 5 };

        FluentActions.Invoking(() => MovingAverageFilter.Apply(input, window: 4))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        double[] output = MovingAverageFilter.Apply(ReadOnlySpan<double>.Empty, window: 5);
        output.Should().BeEmpty();
    }

    [Fact]
    public void NoisyGaussian_SmoothedToAnalyticalAmplitude()
    {
        // Given: noisy single Gaussian (from our TestSignalGenerator)
        var (_, noisy) = Helpers.TestSignalGenerator.GenerateSingleGaussian(
            sampleRateHz: 100.0,
            durationSeconds: 10.0,
            peakAmplitude: 1.0,
            retentionTimeSeconds: 5.0,
            sigmaSeconds: 0.5,
            noiseStdDev: 0.05);

        // When: smooth with a modest window
        double[] smoothed = MovingAverageFilter.Apply(noisy, window: 11);

        // Then: peak amplitude should be closer to true 1.0 than noisy input
        double noisyMax = noisy.Max();
        double smoothedMax = smoothed.Max();

        // Due to noise, noisy peak might deviate ±~0.15 from 1.0
        // Smoothed peak should be within ±0.05
        smoothedMax.Should().BeApproximately(1.0, 0.05);
        noisyMax.Should().BeGreaterThan(1.0 + 0.05); // noisy overshoots
    }
}