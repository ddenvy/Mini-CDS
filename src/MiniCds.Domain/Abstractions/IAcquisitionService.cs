// c:\Develop\Mini-CDS\src\MiniCds.Domain\Abstractions\IAcquisitionService.cs
using MiniCds.Domain.Entities;
using MiniCds.Domain.ValueObjects;

namespace MiniCds.Domain.Abstractions;

/// <summary>
/// Service for orchestrating chromatographic acquisition.
/// </summary>
public interface IAcquisitionService
{
    /// <summary>
    /// Raised when a new SignalFrame is processed.
    /// </summary>
    event EventHandler<SignalFrame>? FrameProcessed;

    /// <summary>
    /// Raised when peaks are detected.
    /// </summary>
    event EventHandler<IReadOnlyList<DetectedPeak>>? PeaksDetected;

    /// <summary>
    /// Current sample being acquired (null if not started).
    /// </summary>
    Sample? CurrentSample { get; }

    /// <summary>
    /// Starts acquisition for the specified sample.
    /// </summary>
    Task StartAsync(long sampleId, IInstrumentSource instrument, long actorUserId, CancellationToken ct = default);

    /// <summary>
    /// Stops acquisition, persists RawSignal + Peak entities.
    /// </summary>
    Task StopAsync(CancellationToken ct = default);
}
