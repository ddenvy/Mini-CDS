// c:\Develop\Mini-CDS\src\MiniCds.Domain\Entities\Peak.cs
using MiniCds.Domain.ValueObjects;

namespace MiniCds.Domain.Entities;

/// <summary>
/// Immutable detected chromatographic peak.
/// Manual re-integration creates NEW peak + audit entry; old peak marked as voided.
/// </summary>
public sealed class Peak
{
    public long Id { get; init; }
    
    public long SampleId { get; init; }
    
    /// <summary>Index of peak apex in the signal array.</summary>
    public int ApexIndex { get; init; }
    
    /// <summary>Start index of peak (where signal rises above baseline).</summary>
    public int StartIndex { get; init; }
    
    /// <summary>End index of peak (where signal returns to baseline).</summary>
    public int EndIndex { get; init; }
    
    /// <summary>All measurable peak properties (RT, Height, Area, FWHM, etc.).</summary>
    public required PeakMetrics Metrics { get; init; }
    
    /// <summary>Timestamp when peak was detected.</summary>
    public DateTime DetectedAtUtc { get; init; }
    
    /// <summary>True if peak was manually re-integrated by user.</summary>
    public bool IsManual { get; init; }
    
    /// <summary>Timestamp when peak was voided (null if not voided).</summary>
    public DateTime? VoidedAtUtc { get; set; }
    
    /// <summary>Reason for voiding (required if VoidedAtUtc is set).</summary>
    public string? VoidedReason { get; set; }
    
    /// <summary>User who voided this peak.</summary>
    public long? VoidedByUserId { get; set; }
    
    // Navigation properties
    public Sample? Sample { get; init; }
    public User? VoidedBy { get; init; }
}