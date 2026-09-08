namespace MiniCds.Domain.ValueObjects;

/// <summary>
/// All measurable properties of a detected chromatographic peak.
/// </summary>
public readonly record struct PeakMetrics
{
    /// <summary>Retention time at apex (seconds).</summary>
    public double RetentionTime { get; init; }

    /// <summary>Peak height above baseline.</summary>
    public double Height { get; init; }

    /// <summary>Integrated peak area (trapezoidal).</summary>
    public double Area { get; init; }

    /// <summary>Width at base (seconds).</summary>
    public double WidthBase { get; init; }

    /// <summary>Full width at half maximum (seconds).</summary>
    public double Fwhm { get; init; }

    /// <summary>Theoretical plates (N = 5.54 * (RT / FWHM)^2).</summary>
    public double? Plates { get; init; }

    /// <summary>Tailing factor (W0.05 / (2 * AS)).</summary>
    public double? Tailing { get; init; }
}