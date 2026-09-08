namespace MiniCds.Domain.Enums;

/// <summary>
/// Real-time instrument connection state.
/// </summary>
public enum InstrumentState
{
    Idle,
    Connecting,
    Streaming,
    Stopping,
    Faulted
}