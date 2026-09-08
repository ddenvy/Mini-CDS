namespace MiniCds.Tests.Helpers;

/// <summary>
/// Deterministic (seeded) signal generator for DSP unit tests.
/// Produces chromatogram-like signals from Gaussian peaks + optional noise + optional baseline tilt.
/// </summary>
public static class TestSignalGenerator
{
    /// <summary>
    /// Generates a single-Gaussian signal: h(t) = A * exp(-(t-t0)² / (2σ²)).
    /// Amplitude A, retention time t0, width σ, sample rate — all configurable.
    /// Ground truth is known analytically, perfect for DSP verification.
    /// </summary>
    /// <param name="sampleRateHz">Sampling frequency (e.g. 10 Hz = one point every 0.1s).</param>
    /// <param name="durationSeconds">Total signal length in seconds.</param>
    /// <param name="peakAmplitude">Height of the Gaussian peak.</param>
    /// <param name="retentionTimeSeconds">Center of the peak (t0).</param>
    /// <param name="sigmaSeconds">Peak standard deviation (width).</param>
    /// <param name="noiseStdDev">Gaussian noise σ. 0 = no noise (perfect signal).</param>
    /// <param name="baselineSlope">Optional linear baseline tilt per second. 0 = flat.</param>
    /// <returns>(times, values) as double arrays.</returns>
    public static (double[] times, double[] values) GenerateSingleGaussian(
        double sampleRateHz,
        double durationSeconds,
        double peakAmplitude,
        double retentionTimeSeconds,
        double sigmaSeconds,
        double noiseStdDev = 0,
        double baselineSlope = 0)
    {
        int n = (int)(sampleRateHz * durationSeconds);
        double dt = 1.0 / sampleRateHz;
        var times = new double[n];
        var values = new double[n];
        var rng = new Random(42); // seeded — deterministic tests

        for (int i = 0; i < n; i++)
        {
            double t = i * dt;
            double gaussian = peakAmplitude * Math.Exp(-(t - retentionTimeSeconds) * (t - retentionTimeSeconds) / (2.0 * sigmaSeconds * sigmaSeconds));
            double baseline = baselineSlope * t;
            double noise = noiseStdDev > 0 ? NextGaussian(rng) * noiseStdDev : 0;
            times[i] = t;
            values[i] = gaussian + baseline + noise;
        }

        return (times, values);
    }

    /// <summary>
    /// Generates a multi-peak signal: sum of Gaussians + noise + baseline.
    /// Each Gaussian is defined by its own (amplitude, retentionTime, sigma).
    /// </summary>
    public static (double[] times, double[] values) GenerateMultiPeak(
        double sampleRateHz,
        double durationSeconds,
        IEnumerable<PeakDefinition> peaks,
        double noiseStdDev = 0,
        double baselineSlope = 0)
    {
        var peakList = peaks.ToList();
        int n = (int)(sampleRateHz * durationSeconds);
        double dt = 1.0 / sampleRateHz;
        var times = new double[n];
        var values = new double[n];
        var rng = new Random(42);

        for (int i = 0; i < n; i++)
        {
            double t = i * dt;
            double sum = 0;
            foreach (var p in peakList)
            {
                sum += p.Amplitude * Math.Exp(-(t - p.RetentionTime) * (t - p.RetentionTime) / (2.0 * p.Sigma * p.Sigma));
            }
            double baseline = baselineSlope * t;
            double noise = noiseStdDev > 0 ? NextGaussian(rng) * noiseStdDev : 0;
            times[i] = t;
            values[i] = sum + baseline + noise;
        }

        return (times, values);
    }

    /// <summary>
    /// Generates pure white noise (Gaussian). Useful for "no peaks found" tests.
    /// </summary>
    public static (double[] times, double[] values) GenerateNoiseOnly(
        double sampleRateHz,
        double durationSeconds,
        double noiseStdDev)
    {
        int n = (int)(sampleRateHz * durationSeconds);
        double dt = 1.0 / sampleRateHz;
        var times = new double[n];
        var values = new double[n];
        var rng = new Random(42);

        for (int i = 0; i < n; i++)
        {
            times[i] = i * dt;
            values[i] = NextGaussian(rng) * noiseStdDev;
        }

        return (times, values);
    }

    /// <summary>Analytical area of a Gaussian: A * σ * √(2π). Useful for peak verification.</summary>
    public static double GaussianArea(double amplitude, double sigma) => amplitude * sigma * Math.Sqrt(2.0 * Math.PI);

    /// <summary>Full width at half maximum of a Gaussian: 2σ√(2ln2) ≈ 2.3548σ.</summary>
    public static double GaussianFwhm(double sigma) => 2.354820045 * sigma;

    private static double NextGaussian(Random rng)
    {
        // Box-Muller transform — numerically stable standard normal samples
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}

/// <summary>Definition of one Gaussian peak inside a multi-peak signal.</summary>
public readonly record struct PeakDefinition(double Amplitude, double RetentionTime, double Sigma);
