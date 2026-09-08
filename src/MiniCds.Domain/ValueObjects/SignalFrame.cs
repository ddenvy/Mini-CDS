namespace MiniCds.Domain.ValueObjects;

/// <summary>
/// Single timestamped data point from an instrument.
/// Immutable, used as the unit of acquisition and DSP pipeline input.
/// </summary>
public readonly record struct SignalFrame(double TimestampSeconds, double Value);