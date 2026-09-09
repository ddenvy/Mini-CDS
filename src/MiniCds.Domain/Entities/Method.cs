// c:\Develop\Mini-CDS\src\MiniCds.Domain\Entities\Method.cs
using MiniCds.Domain.ValueObjects;

namespace MiniCds.Domain.Entities;

/// <summary>
/// Versioned chromatographic acquisition method. Contains DSP parameters and sample rate.
/// Changes to method create audit trail entries (old → new values).
/// </summary>
public sealed class Method
{
    public long Id { get; init; }
    
    /// <summary>Human-readable method name (e.g., "Standard Analysis v2").</summary>
    public required string Name { get; init; }
    
    /// <summary>Method version number. Incremented on each change.</summary>
    public int Version { get; init; } = 1;
    
    /// <summary>DSP and acquisition parameters.</summary>
    public required ProcessingParameters Parameters { get; init; }
    
    public DateTime CreatedAtUtc { get; init; }
    public long CreatedByUserId { get; init; }
    
    /// <summary>Navigation: user who created this method.</summary>
    public User? CreatedBy { get; init; }
    
    /// <summary>Navigation: samples using this method.</summary>
    public IReadOnlyList<Sample>? Samples { get; init; }
}