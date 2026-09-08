namespace MiniCds.Application.SignalProcessing;

/// <summary>
/// Simple moving average (SMA) filter — uniform-weighted smoothing.
/// output[i] = mean(input[i-m .. i+m]), where window = 2m+1.
/// Edges use a smaller window (no reflection/padding bias).
/// </summary>
public static class MovingAverageFilter
{
    /// <summary>
    /// Applies SMA with the given window size. Window must be odd (2m+1).
    /// </summary>
    /// <param name="input">Input signal (contiguous array).</param>
    /// <param name="window">Odd positive integer (e.g. 5, 7, 11).</param>
    /// <returns>New array of same length — smoothed signal.</returns>
    public static double[] Apply(ReadOnlySpan<double> input, int window)
    {
        if (window <= 0 || window % 2 == 0)
            throw new ArgumentOutOfRangeException(nameof(window), "Window must be a positive odd integer.");
        if (input.Length == 0)
            return Array.Empty<double>();

        int n = input.Length;
        int m = window / 2; // radius
        var output = new double[n];

        for (int i = 0; i < n; i++)
        {
            // Clamp window at edges — use actual available samples
            int start = Math.Max(0, i - m);
            int end = Math.Min(n - 1, i + m);
            int count = end - start + 1;

            double sum = 0;
            for (int j = start; j <= end; j++)
                sum += input[j];

            output[i] = sum / count;
        }

        return output;
    }
}