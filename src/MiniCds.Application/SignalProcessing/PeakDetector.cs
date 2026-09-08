using MiniCds.Domain.ValueObjects;
using MiniCds.Domain.Abstractions;

namespace MiniCds.Application.SignalProcessing;

/// <summary>
/// Peak detection on baseline-corrected signal (after smoothing).
/// Pipeline: local maxima → prominence + height + width filters → metrics.
/// </summary>
public static class PeakDetector
{
    /// <summary>
    /// Detects peaks and computes metrics (RT, height, area, width, FWHM, plates, tailing).
    /// Returns DetectedPeak list ordered by apex index.
    /// </summary>
    /// <param name="times">Time axis (seconds), same length as corrected.</param>
    /// <param name="corrected">Baseline-corrected smoothed signal.</param>
    /// <param name="p">Processing parameters for thresholds.</param>
    public static List<DetectedPeak> Detect(ReadOnlySpan<double> times, ReadOnlySpan<double> corrected, ProcessingParameters p)
    {
        int n = corrected.Length;
        var peaks = new List<DetectedPeak>();
        if (n < 3)
            return peaks;

        // Step 1: find all local maxima (strict: strictly greater than both neighbors)
        var candidates = new List<int>();
        for (int i = 1; i < n - 1; i++)
        {
            if (corrected[i] > corrected[i - 1] && corrected[i] > corrected[i + 1])
                candidates.Add(i);
        }

        // Step 2: filter by min height
        var filtered = new List<int>(candidates.Count);
        foreach (int i in candidates)
        {
            if (corrected[i] >= p.MinPeakHeight)
                filtered.Add(i);
        }
        candidates = filtered;

        // Step 3: filter by prominence (height above global minimum on each side).
        // Full signal extent (0..apex and apex..n-1), not just between adjacent
        // candidates — otherwise valleys hidden between non-adjacent peaks get missed
        // and prominence is overestimated, causing false positive peak detection.
        // Prefix/suffix minima: O(n) precompute replaces O(m·n) per-candidate rescan.
        double[] leftMin = new double[n];
        leftMin[0] = corrected[0];
        for (int i = 1; i < n; i++)
            leftMin[i] = Math.Min(corrected[i], leftMin[i - 1]);

        double[] rightMin = new double[n];
        rightMin[n - 1] = corrected[n - 1];
        for (int i = n - 2; i >= 0; i--)
            rightMin[i] = Math.Min(corrected[i], rightMin[i + 1]);

        for (int ci = 0; ci < candidates.Count; ci++)
        {
            int apex = candidates[ci];
            double apexHeight = corrected[apex];

            // Lowest point in the ENTIRE signal left/right of apex (apex ∈ [1, n-2])
            double leftValley = leftMin[apex - 1];
            double rightValley = rightMin[apex + 1];

            double prominence = apexHeight - Math.Max(leftValley, rightValley);
            if (prominence < p.MinProminence)
                continue;

            // Step 4: find peak boundaries (walk down from apex to nearest valley or zero-crossing)
            int start = apex;
            while (start > 0 && corrected[start - 1] >= corrected[start] && corrected[start] > 0)
                start--;
            while (start > 0 && corrected[start - 1] > 0)
                start--;

            int end = apex;
            while (end < n - 1 && corrected[end + 1] >= corrected[end] && corrected[end] > 0)
                end++;
            while (end < n - 1 && corrected[end + 1] > 0)
                end++;

            // Step 5: compute metrics
            PeakMetrics metrics = ComputeMetrics(times, corrected, apex, start, end);

            // Step 6: width filter
            double widthSeconds = times[end] - times[start];
            int widthPoints = end - start + 1;
            if (widthPoints < p.MinWidthPoints)
                continue;

            peaks.Add(new DetectedPeak(apex, start, end, metrics));
        }

        return peaks;
    }

    private static PeakMetrics ComputeMetrics(ReadOnlySpan<double> times, ReadOnlySpan<double> corrected,
                                              int apex, int start, int end)
    {
        double dt = times.Length > 1 ? times[1] - times[0] : 1.0;
        double retentionTime = times[apex];
        double height = corrected[apex];

        // Area = trapezoidal over [start, end]
        double area = 0;
        for (int i = start; i < end; i++)
            area += 0.5 * (corrected[i] + corrected[i + 1]) * dt;

        double widthBase = times[end] - times[start];

        // FWHM: interpolate where signal crosses height/2 on left and right.
        // Two independent searches — missing left crossing no longer skips right search.
        double half = height * 0.5;
        double? fwhm = null;

        // Left crossing: search from apex leftwards down to start.
        double? tLeft = null;
        for (int i = apex; i > start; i--)
        {
            if (corrected[i - 1] <= half && corrected[i] >= half)
            {
                tLeft = times[i] + (half - corrected[i]) / (corrected[i - 1] - corrected[i]) * dt;
                break;
            }
        }

        // Right crossing: search from apex rightwards up to end.
        double? tRight = null;
        for (int j = apex; j < end; j++)
        {
            if (corrected[j] >= half && corrected[j + 1] <= half)
            {
                tRight = times[j] + (half - corrected[j]) / (corrected[j + 1] - corrected[j]) * dt;
                break;
            }
        }

        if (tLeft.HasValue && tRight.HasValue)
            fwhm = tRight.Value - tLeft.Value;

        double? plates = null;
        if (fwhm.HasValue && fwhm.Value > 0)
            plates = 5.54 * (retentionTime / fwhm.Value) * (retentionTime / fwhm.Value);

        return new PeakMetrics
        {
            RetentionTime = retentionTime,
            Height = height,
            Area = area,
            WidthBase = widthBase,
            Fwhm = fwhm ?? 0,
            Plates = plates
        };
    }
}