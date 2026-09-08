using MiniCds.Domain.Enums;
using MiniCds.Domain.ValueObjects;

namespace MiniCds.Domain.Abstractions;

/// <summary>
/// Contract for any device that produces SignalFrame data.
/// Implementations: SimulatorInstrumentSource, MqttInstrumentSource.
/// </summary>
public interface IInstrumentSource : IAsyncDisposable
{
    InstrumentState State { get; }
    event EventHandler<SignalFrame>? FrameReceived;
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}