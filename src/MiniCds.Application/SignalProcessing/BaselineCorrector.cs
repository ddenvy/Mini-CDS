namespace MiniCds.Application.SignalProcessing;

/// <summary>
/// Asymmetric Least Squares (ALS / Whittaker) baseline correction.
/// Iteratively solves a penalized least-squares problem to fit a smooth baseline:
///   min Σ wᵢ(yᵢ − zᵢ)² + λ·Σ(Δ²z)²,  wᵢ = p if yᵢ > zᵢ else (1−p)
/// z = fitted baseline, correction = y − z.
/// </summary>
public static class BaselineCorrector
{
    /// <summary>
    /// Computes the ALS baseline z for input signal y.
    /// </summary>
    /// <param name="input">Smoothed input signal.</param>
    /// <param name="lambda">Smoothness penalty (typical: 1e5..1e7 for HPLC).</param>
    /// <param name="p">Asymmetry parameter (typical: 0.001..0.01).</param>
    /// <param name="maxIterations">ALS iterations (default 15).</param>
    /// <returns>Fitted baseline z (same length as input).</returns>
    public static double[] FitBaseline(ReadOnlySpan<double> input, double lambda = 1e6, double p = 0.01, int maxIterations = 15)
    {
        int n = input.Length;
        if (n < 3)
            return input.ToArray();

        // Constant bands of DᵀD (Whittaker second-difference penalty), built in O(n):
        // main diagonal: 1,5,6,...,6,5,1; first subdiagonal: -2,-4,...,-4,-2; second: all +1
        double[] d0 = new double[n];
        double[] d1 = new double[n - 1];
        double[] d2 = new double[n - 2];
        for (int i = 0; i < n; i++)
            d0[i] = (i <= n - 3 ? 1.0 : 0.0) + (i >= 1 && i <= n - 2 ? 4.0 : 0.0) + (i >= 2 ? 1.0 : 0.0);
        for (int j = 0; j < n - 1; j++)
            d1[j] = (j <= n - 3 ? -2.0 : 0.0) + (j >= 1 ? -2.0 : 0.0);
        Array.Fill(d2, 1.0);

        // Weight vector w, initialized to ones; working arrays reused across iterations
        double[] w = new double[n];
        Array.Fill(w, 1.0);
        double[] a0 = new double[n];
        double[] a1 = new double[n - 1];
        double[] a2 = new double[n - 2];
        double[] z = new double[n];

        // A = W + λ·DᵀD is SPD pentadiagonal -> O(n) banded Cholesky.
        // No O(n²) dense allocation, no MathNet sparse (which needs a native provider anyway).
        for (int iter = 0; iter < maxIterations; iter++)
        {
            for (int i = 0; i < n; i++)
            {
                a0[i] = w[i] + lambda * d0[i];
                z[i] = w[i] * input[i]; // rhs = W·y, overwritten by the solution
            }
            for (int j = 0; j < n - 1; j++)
                a1[j] = lambda * d1[j];
            Array.Fill(a2, lambda);

            SolvePentadiagonal(a0, a1, a2, z);

            for (int i = 0; i < n; i++)
                w[i] = input[i] > z[i] ? p : 1.0 - p;
        }

        return z;
    }

    /// <summary>
    /// Solves A·x = b in place (b is overwritten with x) for a symmetric positive definite
    /// pentadiagonal matrix given by three bands: a0 (main, len n), a1 (±1, len n-1), a2 (±2, len n-2).
    /// Banded LDLᵀ factorization: O(n) time, no matrix objects — flat arrays only (DOD-friendly).
    /// </summary>
    private static void SolvePentadiagonal(double[] a0, double[] a1, double[] a2, double[] b)
    {
        int n = a0.Length;
        // Unit-diagonal lower factor bands: l1[i] = L[i+1,i], l2[i] = L[i+2,i]; pivots overwrite a0
        double[] l1 = new double[n - 1];
        double[] l2 = new double[n - 2];

        for (int j = 0; j < n; j++)
        {
            double d = a0[j];
            if (j >= 1) d -= l1[j - 1] * l1[j - 1] * a0[j - 1];
            if (j >= 2) d -= l2[j - 2] * l2[j - 2] * a0[j - 2];
            a0[j] = d;

            if (j < n - 1)
            {
                double s = a1[j];
                if (j >= 1) s -= l2[j - 1] * l1[j - 1] * a0[j - 1];
                l1[j] = s / d;
            }
            if (j < n - 2)
                l2[j] = a2[j] / d;
        }

        // Forward substitution: y = L⁻¹·b
        for (int i = 0; i < n; i++)
        {
            double s = b[i];
            if (i >= 1) s -= l1[i - 1] * b[i - 1];
            if (i >= 2) s -= l2[i - 2] * b[i - 2];
            b[i] = s;
        }
        // Back substitution: x = D⁻¹·Lᵀ⁻¹·y (walk upwards, scale by pivot, eliminate)
        for (int i = n - 1; i >= 0; i--)
        {
            b[i] /= a0[i];
            if (i < n - 1) b[i] -= l1[i] * b[i + 1];
            if (i < n - 2) b[i] -= l2[i] * b[i + 2];
        }
    }

    /// <summary>
    /// Convenience: compute corrected = input − baseline.
    /// </summary>
    public static double[] Correct(ReadOnlySpan<double> input, double lambda = 1e6, double p = 0.01, int maxIterations = 15)
    {
        double[] baseline = FitBaseline(input, lambda, p, maxIterations);
        var corrected = new double[input.Length];
        for (int i = 0; i < input.Length; i++)
            corrected[i] = input[i] - baseline[i];
        return corrected;
    }
}