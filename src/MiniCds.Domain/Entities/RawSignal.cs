// c:\Develop\Mini-CDS\src\MiniCds.Domain\Entities\RawSignal.cs
namespace MiniCds.Domain.Entities;

/// <summary>
/// Immutable raw signal data captured from instrument.
/// "Original" copy for ALCOA+ compliance — never modified, only appended.
/// </summary>
public sealed class RawSignal
{
    public long Id { get; init; }
    
    public long SampleId { get; init; }
    
    /// <summary>Timestamp when signal capture started.</summary>
    public DateTime CapturedAtUtc { get; init; }
    
    /// <summary>Sample rate in Hz (points per second).</summary>
    public int SampleRateHz { get; init; }
    
    /// <summary>Raw signal points as binary data (double[] serialized).</summary>
    public required byte[] Points { get; init; }
    
    // Navigation properties
    public Sample? Sample { get; init; }
}