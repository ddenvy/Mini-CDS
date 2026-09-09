// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Instruments\SimulatorInstrumentSourceTests.cs
using FluentAssertions;
using MiniCds.Domain.Enums;
using MiniCds.Domain.ValueObjects;
using MiniCds.Infrastructure.Instruments;

namespace MiniCds.Tests.Instruments;

public class SimulatorInstrumentSourceTests
{
    [Fact]
    public async Task StartAsync_TransitionsStateToStreaming()
    {
        var peaks = new List<SimulatorPeakDefinition>
        {
            new(Amplitude: 100, RetentionTime: 5.0, Sigma: 1.0)
        };
        await using var simulator = new SimulatorInstrumentSource(
            sampleRateHz: 10,
            durationSeconds: 10.0,
            peaks: peaks);

        simulator.State.Should().Be(InstrumentState.Idle);

        await simulator.StartAsync();

        simulator.State.Should().Be(InstrumentState.Streaming);

        await simulator.StopAsync();
    }

    [Fact]
    public async Task StopAsync_TransitionsStateToIdle()
    {
        var peaks = new List<SimulatorPeakDefinition>
        {
            new(Amplitude: 100, RetentionTime: 5.0, Sigma: 1.0)
        };
        await using var simulator = new SimulatorInstrumentSource(
            sampleRateHz: 10,
            durationSeconds: 10.0,
            peaks: peaks);

        await simulator.StartAsync();
        await simulator.StopAsync();

        simulator.State.Should().Be(InstrumentState.Idle);
    }

    [Fact]
    public async Task FrameReceived_RaisesEvent_WithSignalFrames()
    {
        var peaks = new List<SimulatorPeakDefinition>
        {
            new(Amplitude: 100, RetentionTime: 0.5, Sigma: 0.1)
        };
        await using var simulator = new SimulatorInstrumentSource(
            sampleRateHz: 10,
            durationSeconds: 1.0,
            peaks: peaks);

        var frames = new List<SignalFrame>();
        simulator.FrameReceived += (_, frame) => frames.Add(frame);

        await simulator.StartAsync();
        await Task.Delay(1200); // Wait for 1 second of data
        await simulator.StopAsync();

        frames.Should().HaveCountGreaterThanOrEqualTo(10);
        frames[0].TimestampSeconds.Should().BeApproximately(0.0, 0.01);
    }

    [Fact]
    public async Task FrameReceived_GeneratesGaussianPeak_AtRetentionTime()
    {
        var peaks = new List<SimulatorPeakDefinition>
        {
            new(Amplitude: 100, RetentionTime: 0.5, Sigma: 0.1)
        };
        await using var simulator = new SimulatorInstrumentSource(
            sampleRateHz: 10,
            durationSeconds: 1.0,
            peaks: peaks);

        var frames = new List<SignalFrame>();
        simulator.FrameReceived += (_, frame) => frames.Add(frame);

        await simulator.StartAsync();
        await Task.Delay(1200);
        await simulator.StopAsync();

        var peakFrame = frames.MaxBy(f => f.Value);
        peakFrame.Should().NotBeNull();
        peakFrame!.TimestampSeconds.Should().BeApproximately(0.5, 0.1);
        peakFrame.Value.Should().BeGreaterThan(90);
    }

    [Fact]
    public async Task StartAsync_CalledTwice_ThrowsInvalidOperationException()
    {
        var peaks = new List<SimulatorPeakDefinition>
        {
            new(Amplitude: 100, RetentionTime: 5.0, Sigma: 1.0)
        };
        await using var simulator = new SimulatorInstrumentSource(
            sampleRateHz: 10,
            durationSeconds: 10.0,
            peaks: peaks);

        await simulator.StartAsync();

        var act = () => simulator.StartAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulator already started.");

        await simulator.StopAsync();
    }

    [Fact]
    public async Task DisposeAsync_StopsStreaming()
    {
        var peaks = new List<SimulatorPeakDefinition>
        {
            new(Amplitude: 100, RetentionTime: 5.0, Sigma: 1.0)
        };
        var simulator = new SimulatorInstrumentSource(
            sampleRateHz: 10,
            durationSeconds: 10.0,
            peaks: peaks);

        await simulator.StartAsync();
        simulator.State.Should().Be(InstrumentState.Streaming);

        await simulator.DisposeAsync();

        simulator.State.Should().Be(InstrumentState.Idle);
    }
}