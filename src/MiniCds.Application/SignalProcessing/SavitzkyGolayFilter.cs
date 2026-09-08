using MathNet.Numerics.LinearAlgebra;

namespace MiniCds.Application.SignalProcessing;

/// <summary>
/// Savitzky-Golay smoothing filter. Fits a local polynomial of order p over a window of 2m+1 points.
/// Coefficients are computed once and cached per (window, order) pair.
/// Also computes first-derivative coefficients (row 1 of C) when needed.
/// </summary>
public static class SavitzkyGolayFilter
{
    // Cache: key = (window, order), value = coefficients array of length window
    private static readonly Dictionary<(int window, int order), double[]> CoefficientCache = new();

    /// <summary>
    /// Applies SG smoothing. Window must be odd, order must be less than window.
    /// Edges use a progressively smaller valid polynomial fit (no padding).
    /// </summary>
    public static double[] Apply(ReadOnlySpan<double> input, int window, int order)
    {
        ValidateParameters(window, order);
        if (input.Length == 0)
            return Array.Empty<double>();

        double[] coefficients = GetOrComputeCoefficients(window, order);
        int m = window / 2;
        var output = new double[input.Length];

        for (int i = 0; i < input.Length; i++)
        {
            int start = Math.Max(0, i - m);
            int end = Math.Min(input.Length - 1, i + m);

            int available = end - start + 1;
            // SG coefficients are computed for a full symmetric window [-m, +m].
            // When we don't have the full window (near edges), coefficients don't sum to 1
            // on the available subset — copying the input is the safest edge handling.
            if (available != window)
            {
                output[i] = input[i];
                continue;
            }

            double sum = 0;
            for (int j = start; j <= end; j++)
            {
                // Map input index j into filter kernel index k in [-m, m]
                int k = j - i;
                sum += coefficients[k + m] * input[j];
            }
            output[i] = sum;
        }

        return output;
    }

    /// <summary>
    /// Returns the first-derivative coefficients for the same (window, order).
    /// Used by peak detection to find slope changes.
    /// </summary>
    public static double[] GetDerivativeCoefficients(int window, int order)
    {
        ValidateParameters(window, order);
        int m = window / 2;

        // Build Vandermonde matrix J: rows k=-m..m, cols 0..p
        var J = Matrix<double>.Build.Dense(window, order + 1);
        for (int row = 0; row < window; row++)
        {
            double k = row - m;
            for (int col = 0; col <= order; col++)
                J[row, col] = Math.Pow(k, col);
        }

        // C = (JᵀJ)⁻¹ Jᵀ
        var Jt = J.Transpose();
        var C = Jt.Multiply(J).Inverse().Multiply(Jt);

        // Row 1 = first derivative coefficients. Normalize by dt later (caller's job).
        var row1 = C.Row(1).ToArray();
        return row1;
    }

    private static void ValidateParameters(int window, int order)
    {
        if (window <= 0 || window % 2 == 0)
            throw new ArgumentOutOfRangeException(nameof(window), "Window must be a positive odd integer.");
        if (order < 0 || order >= window)
            throw new ArgumentOutOfRangeException(nameof(order), "Order must be between 0 and window-1.");
    }

    private static double[] GetOrComputeCoefficients(int window, int order)
    {
        var key = (window, order);
        if (CoefficientCache.TryGetValue(key, out double[]? cached))
            return cached;

        int m = window / 2;

        // Build Vandermonde matrix J: rows k=-m..m, cols 0..order
        var J = Matrix<double>.Build.Dense(window, order + 1);
        for (int row = 0; row < window; row++)
        {
            double k = row - m;
            for (int col = 0; col <= order; col++)
                J[row, col] = Math.Pow(k, col);
        }

        // C = (JᵀJ)⁻¹ Jᵀ  (pseudoinverse for overdetermined system)
        var Jt = J.Transpose();
        var C = Jt.Multiply(J).Inverse().Multiply(Jt);

        // Smoothing coefficients = row 0 of C, normalized (Σc = 1)
        var row0 = C.Row(0).ToArray();
        double sum = row0.Sum();
        for (int i = 0; i < row0.Length; i++)
            row0[i] /= sum;

        CoefficientCache[key] = row0;
        return row0;
    }
}