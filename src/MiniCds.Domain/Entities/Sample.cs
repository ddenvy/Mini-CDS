// c:\Develop\Mini-CDS\src\MiniCds.Domain\Entities\Sample.cs
using MiniCds.Domain.Enums;

namespace MiniCds.Domain.Entities;

/// <summary>
/// Chromatographic sample. Lifecycle: Queued → Running → Completed.
/// Void instead of delete (21 CFR Part 11 compliance).
/// </summary>
public sealed class Sample
{
    public long Id { get; init; }
    
    /// <summary>Sample name or identifier (e.g., "Sample_001").</summary>
    public required string Name { get; init; }
    
    public long MethodId { get; init; }
    
    /// <summary>Current lifecycle status.</summary>
    public SampleStatus Status { get; set; } = SampleStatus.Queued;
    
    public DateTime CreatedAtUtc { get; init; }
    public long CreatedByUserId { get; init; }
    
    /// <summary>Timestamp when sample was voided (null if not voided).</summary>
    public DateTime? VoidedAtUtc { get; set; }
    
    /// <summary>Reason for voiding (required if VoidedAtUtc is set).</summary>
    public string? VoidedReason { get; set; }
    
    /// <summary>User who voided this sample.</summary>
    public long? VoidedByUserId { get; set; }
    
    // Navigation properties
    public Method? Method { get; init; }
    public User? CreatedBy { get; init; }
    public User? VoidedBy { get; init; }
    
    /// <summary>Navigation: raw signals captured for this sample.</summary>
    public IReadOnlyList<RawSignal>? RawSignals { get; init; }
    
    /// <summary>Navigation: peaks detected in this sample.</summary>
    public IReadOnlyList<Peak>? Peaks { get; init; }
}