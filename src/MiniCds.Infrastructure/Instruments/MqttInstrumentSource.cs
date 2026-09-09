using System.Text.Json;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Enums;
using MiniCds.Domain.ValueObjects;
using MQTTnet;
using MQTTnet.Client;

namespace MiniCds.Infrastructure.Instruments;

/// <summary>
/// Reads chromatographic signal frames from an MQTT broker.
/// Topic: instrument/{deviceId}/signal
/// Payload formats (JSON):
///   - single point: {"t": 12.34, "v": 0.567}
///   - batch: {"rate": 10, "t0": 0.0, "v": [0.11, 0.12, ...]}
/// </summary>
public sealed class MqttInstrumentSource : IInstrumentSource
{
    private readonly string _brokerHost;
    private readonly int _brokerPort;
    private readonly string _deviceId;
    private readonly string _topic;
    private IMqttClient? _client;
    private CancellationTokenSource? _cts;

    public MqttInstrumentSource(string brokerHost, int brokerPort, string deviceId)
    {
        _brokerHost = brokerHost;
        _brokerPort = brokerPort;
        _deviceId = deviceId;
        _topic = $"instrument/{_deviceId}/signal";
    }

    public InstrumentState State { get; private set; } = InstrumentState.Idle;

    public event EventHandler<SignalFrame>? FrameReceived;

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_client is not null)
            throw new InvalidOperationException("MQTT source already started.");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_brokerHost, _brokerPort)
            .WithClientId($"MiniCDS-{_deviceId}-{Guid.NewGuid():N}")
            .WithCleanSession()
            .Build();

        await _client.ConnectAsync(options, _cts.Token);

        var topicFilter = new MqttTopicFilterBuilder()
            .WithTopic(_topic)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _client.SubscribeAsync(topicFilter, _cts.Token);

        State = InstrumentState.Streaming;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_client is null) return;

        State = InstrumentState.Stopping;

        try
        {
            await _client.UnsubscribeAsync(_topic, ct);
            await _client.DisconnectAsync(cancellationToken: ct);
        }
        catch
        {
            // Ignore disconnect errors during shutdown
        }

        _client.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
        _client.Dispose();
        _client = null;
        _cts?.Dispose();
        _cts = null;

        State = InstrumentState.Idle;
    }

    public async ValueTask DisposeAsync()
    {
        if (State == InstrumentState.Streaming)
        {
            await StopAsync();
        }
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var payload = e.ApplicationMessage.PayloadSegment;
            if (payload.Count == 0) return Task.CompletedTask;

            var json = System.Text.Encoding.UTF8.GetString(payload.Array!, payload.Offset, payload.Count);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("v", out var vProp) && vProp.ValueKind == JsonValueKind.Array)
            {
                // Batch format: {"rate": 10, "t0": 0.0, "v": [0.11, 0.12, ...]}
                double rate = root.TryGetProperty("rate", out var rateProp) ? rateProp.GetDouble() : 1.0;
                double t0 = root.TryGetProperty("t0", out var t0Prop) ? t0Prop.GetDouble() : 0.0;
                double dt = 1.0 / rate;

                int index = 0;
                foreach (var value in vProp.EnumerateArray())
                {
                    double t = t0 + index * dt;
                    double v = value.GetDouble();
                    FrameReceived?.Invoke(this, new SignalFrame(t, v));
                    index++;
                }
            }
            else if (root.TryGetProperty("t", out var tProp) && root.TryGetProperty("v", out var vSingleProp))
            {
                // Single point format: {"t": 12.34, "v": 0.567}
                double t = tProp.GetDouble();
                double v = vSingleProp.GetDouble();
                FrameReceived?.Invoke(this, new SignalFrame(t, v));
            }
        }
        catch (JsonException)
        {
            // Ignore malformed messages
        }

        return Task.CompletedTask;
    }
}
