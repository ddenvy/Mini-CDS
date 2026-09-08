using FluentAssertions;
using MiniCds.Application.SignalProcessing;

namespace MiniCds.Tests.SignalProcessing;

public class SavitzkyGolayFilterTests
{
    [Fact]
    public void ConstantSignal_PassesUnchanged()
    {
        double[] input = Enumerable.Repeat(3.14, 100).ToArray();
        double[] output = SavitzkyGolayFilter.Apply(input, window: 5, order: 2);
        output.Should().AllSatisfy(x => x.Should().BeApproximately(3.14, 1e-9));
    }

    [Fact]
    public void LinearSignal_PassesUnchanged()
    {
        // Linear function (degree 1 polynomial) — SG order >= 1 must preserve it exactly
        var input = new double[50];
        for (int i = 0; i < input.Length; i++)
            input[i] = 2.0 * i + 1.5; // y = 2x + 1.5

        double[] output = SavitzkyGolayFilter.Apply(input, window: 5, order: 2);

        // Skip edges (where window is not full)
        int skip = 2; // m = window/2
        for (int i = skip; i < input.Length - skip; i++)
            output[i].Should().BeApproximately(input[i], 1e-9);
    }

    [Fact]
    public void QuadraticSignal_PassesUnchanged_StrongTest()
    {
        // Quadratic function (degree 2) — SG order >= 2 must preserve it exactly
        // This is the classic "strong test" from the plan §5.2
        var input = new double[100];
        for (int i = 0; i < input.Length; i++)
            input[i] = 0.5 * i * i - 3.0 * i + 10.0; // y = 0.5x² - 3x + 10

        double[] output = SavitzkyGolayFilter.Apply(input, window: 5, order: 2);

        int skip = 2;
        for (int i = skip; i < input.Length - skip; i++)
            output[i].Should().BeApproximately(input[i], 1e-9);
    }

    [Fact]
    public void NoisyGaussian_BothFiltersApproximateTruePeakBetterThanRaw()
    {
        var (_, noisy) = Helpers.TestSignalGenerator.GenerateSingleGaussian(
            sampleRateHz: 100.0,
            durationSeconds: 10.0,
            peakAmplitude: 1.0,
            retentionTimeSeconds: 5.0,
            sigmaSeconds: 0.5,
            noiseStdDev: 0.1);

        // Same window for both — SG should preserve peak shape better
        double[] maSmoothed = MovingAverageFilter.Apply(noisy, window: 11);
        double[] sgSmoothed = SavitzkyGolayFilter.Apply(noisy, window: 11, order: 3);

        double noisyMax = noisy.Max();
        double maMax = maSmoothed.Max();
        double sgMax = sgSmoothed.Max();

        // Both filters should reduce the noisy overshoot toward true 1.0
        maMax.Should().BeLessThan(noisyMax);
        sgMax.Should().BeLessThan(noisyMax);

        // SG should not be drastically worse than MA at recovering peak amplitude
        // (due to noise, the closer one varies run-to-run; neither should be off by >0.1)
        Math.Abs(sgMax - 1.0).Should().BeLessThan(0.10);
        Math.Abs(maMax - 1.0).Should().BeLessThan(0.10);
    }

    [Fact]
    public void EvenWindow_Throws()
    {
        double[] input = { 1, 2, 3, 4, 5 };
        FluentActions.Invoking(() => SavitzkyGolayFilter.Apply(input, window: 4, order: 2))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void OrderEqualsWindow_Throws()
    {
        double[] input = { 1, 2, 3, 4, 5 };
        // order must be < window
        FluentActions.Invoking(() => SavitzkyGolayFilter.Apply(input, window: 5, order: 5))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DerivativeCoefficients_SumToZero()
    {
        // First-derivative SG coefficients always sum to 0
        // (derivative of a constant is zero)
        double[] deriv = SavitzkyGolayFilter.GetDerivativeCoefficients(window: 7, order: 3);
        deriv.Sum().Should().BeApproximately(0, 1e-9);
    }
}