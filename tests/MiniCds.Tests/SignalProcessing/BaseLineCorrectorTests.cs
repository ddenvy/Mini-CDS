using FluentAssertions;
using MiniCds.Application.SignalProcessing;

namespace MiniCds.Tests.SignalProcessing;

public class BaselineCorrectorTests
{
    [Fact]
    public void FlatSignal_BaselineIsAlmostZero()
    {
        // Given: flat signal y = 0 everywhere (no baseline, no peaks)
        double[] input = new double[100];

        // When
        double[] baseline = BaselineCorrector.FitBaseline(input);

        // Then: baseline should be near zero everywhere
        baseline.All(x => Math.Abs(x) < 1e-6).Should().BeTrue();
    }

    [Fact]
    public void LinearBaseline_CorrectedIsFlatInPeakFreeZones()
    {
        // Given: signal = weak linear baseline + strong peak
        // ALS should subtract the linear trend, leaving only the peak above ~0
        var input = new double[200];
        for (int i = 0; i < input.Length; i++)
        {
            double t = i;
            double baseline = 0.05 * t;           // weak linear slope
            double peak = 2.0 * Math.Exp(-(t - 100.0) * (t - 100.0) / 50.0);
            input[i] = baseline + peak;
        }

        // When — more iterations for convergence
        double[] corrected = BaselineCorrector.Correct(input, lambda: 1e5, p: 0.05, maxIterations: 15);

        // Then: in peak-free zones (far from apex), corrected should be SIGNIFICANTLY closer
        // to 0 than the original tilted signal
        for (int i = 0; i < 30; i++)
        {
            double originalBaseline = 0.05 * i;
            // Corrected value should be less than 25% of original baseline
            Math.Abs(corrected[i]).Should().BeLessThan(Math.Abs(originalBaseline) * 0.25 + 0.2);
        }
        for (int i = 170; i < 200; i++)
        {
            double originalBaseline = 0.05 * i;
            Math.Abs(corrected[i]).Should().BeLessThan(Math.Abs(originalBaseline) * 0.25 + 0.2);
        }

        // Peak should still be visible and near original amplitude
        corrected.Max().Should().BeGreaterThan(1.0);
    }

    [Fact]
    public void EmptyOrShortInput_ReturnsCopy()
    {
        double[] input = { 1.0, 2.0 };
        double[] baseline = BaselineCorrector.FitBaseline(input);
        baseline.Should().BeEquivalentTo(input);
    }

    [Fact]
    public void SmoothedGaussianWithTilt_PeakPreservedAfterPipeline()
    {
        // Important: MA smoothing (window=5) + ALS baseline correction (p=0.05) BOTH reduce peak height.
        // We verify the peak is STILL PRESERVED as a dominant local maximum above threshold.
        var (_, noisy) = Helpers.TestSignalGenerator.GenerateSingleGaussian(
            sampleRateHz: 100.0,
            durationSeconds: 10.0,
            peakAmplitude: 3.0,
            retentionTimeSeconds: 5.0,
            sigmaSeconds: 0.4,
            noiseStdDev: 0.0,
            baselineSlope: 0.2);

        double[] smoothed = MovingAverageFilter.Apply(noisy, window: 5);
        double[] corrected = BaselineCorrector.Correct(smoothed, lambda: 1e5, p: 0.01);

        // Peak height after smoothing/baseline correction ≈ 70-100% of original
        corrected.Max().Should().BeGreaterThan(1.5);
    }

    /// <summary>
    /// Regression guard for the banded DᵀD coefficient formula inside BaselineCorrector.
    /// Compares the O(n) pentadiagonal solver against an O(n³) dense ground truth
    /// (explicit D matrix, DᵀD by multiplication, Gaussian elimination) for every n in 3..32.
    /// Any wrong band coefficient (e.g. d0[1]=4 instead of 5) makes the outputs diverge.
    /// </summary>
    [Fact]
    public void FitBaseline_BandedCoefficients_MatchDenseReference()
    {
        var rng = new Random(42);
        for (int n = 3; n <= 32; n++)
        {
            double sigma = Math.Max(1.0, n / 6.0);
            double[] input = new double[n];
            for (int i = 0; i < n; i++)
                input[i] = 0.1 * i
                    + 3.0 * Math.Exp(-(i - n / 2.0) * (i - n / 2.0) / (2.0 * sigma * sigma))
                    + rng.NextDouble() * 0.05;

            double[] actual = BaselineCorrector.FitBaseline(input, lambda: 1e4, p: 0.01, maxIterations: 15);
            double[] expected = FitBaselineDenseReference(input, lambda: 1e4, p: 0.01, maxIterations: 15);

            for (int i = 0; i < n; i++)
                actual[i].Should().BeApproximately(expected[i], 1e-8,
                    $"n={n}, index={i}: banded DᵀD coefficients diverged from dense ground truth");
        }
    }

    private static double[] FitBaselineDenseReference(double[] y, double lambda, double p, int maxIterations)
    {
        int n = y.Length;

        // Ground-truth DᵀD: explicit accumulation of the (n-2)×n second-difference matrix,
        // row r has stencil [1, -2, 1] at columns r..r+2
        double[,] penalty = new double[n, n];
        for (int r = 0; r <= n - 3; r++)
            for (int ci = 0; ci < 3; ci++)
                for (int cj = 0; cj < 3; cj++)
                {
                    double si = ci == 1 ? -2.0 : 1.0;
                    double sj = cj == 1 ? -2.0 : 1.0;
                    penalty[r + ci, r + cj] += si * sj;
                }

        double[] w = new double[n];
        Array.Fill(w, 1.0);
        double[] z = new double[n];

        for (int iter = 0; iter < maxIterations; iter++)
        {
            double[,] a = new double[n, n];
            double[] rhs = new double[n];
            for (int i = 0; i < n; i++)
            {
                rhs[i] = w[i] * y[i];
                for (int j = 0; j < n; j++)
                    a[i, j] = lambda * penalty[i, j] + (i == j ? w[i] : 0.0);
            }

            z = SolveDense(a, rhs);

            for (int i = 0; i < n; i++)
                w[i] = y[i] > z[i] ? p : 1.0 - p;
        }

        return z;
    }

    private static double[] SolveDense(double[,] a, double[] b)
    {
        int n = b.Length;
        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            for (int r = col + 1; r < n; r++)
                if (Math.Abs(a[r, col]) > Math.Abs(a[pivot, col]))
                    pivot = r;
            if (pivot != col)
            {
                for (int c = 0; c < n; c++)
                    (a[col, c], a[pivot, c]) = (a[pivot, c], a[col, c]);
                (b[col], b[pivot]) = (b[pivot], b[col]);
            }
            for (int r = col + 1; r < n; r++)
            {
                double f = a[r, col] / a[col, col];
                if (f == 0.0)
                    continue;
                for (int c = col; c < n; c++)
                    a[r, c] -= f * a[col, c];
                b[r] -= f * b[col];
            }
        }

        double[] x = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double s = b[i];
            for (int j = i + 1; j < n; j++)
                s -= a[i, j] * x[j];
            x[i] = s / a[i, i];
        }
        return x;
    }
}