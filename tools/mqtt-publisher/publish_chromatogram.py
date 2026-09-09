#!/usr/bin/env python3
"""
MQTT chromatogram publisher for Mini-CDS.

Publishes simulated chromatographic signal to an MQTT broker.
Topic: instrument/{device_id}/signal

Payload formats:
  - single point: {"t": 12.34, "v": 0.567}
  - batch: {"rate": 10, "t0": 0.0, "v": [0.11, 0.12, ...]}

Usage:
  python publish_chromatogram.py [--mode batch|single] [--broker localhost] [--port 1883]
                                 [--device device-001] [--duration 60] [--rate 10]
"""

import argparse
import json
import math
import random
import sys
import time

try:
    import paho.mqtt.client as mqtt
except ImportError:
    print("paho-mqtt is required. Install with: pip install paho-mqtt", file=sys.stderr)
    sys.exit(1)


def gaussian(t, rt, sigma, amplitude):
    """Gaussian peak value at time t."""
    exponent = -((t - rt) ** 2) / (2.0 * sigma ** 2)
    return amplitude * math.exp(exponent)


def generate_chromatogram(duration, rate, noise_std, baseline_slope, peaks):
    """Generate chromatogram points as (time, value) tuples."""
    rng = random.Random(42)
    dt = 1.0 / rate
    total_points = int(duration * rate)

    for i in range(total_points):
        t = i * dt
        value = 0.0

        # Sum of Gaussian peaks
        for rt, sigma, amplitude in peaks:
            value += gaussian(t, rt, sigma, amplitude)

        # Baseline drift
        value += baseline_slope * t

        # Noise
        if noise_std > 0:
            value += rng.gauss(0, noise_std)

        yield t, value


def on_connect(client, userdata, flags, rc, properties=None):
    if rc == 0:
        print(f"Connected to MQTT broker at {userdata['broker']}:{userdata['port']}")
    else:
        print(f"Connection failed with code {rc}", file=sys.stderr)
        sys.exit(1)


def main():
    parser = argparse.ArgumentParser(description="Publish chromatogram data via MQTT")
    parser.add_argument("--broker", default="localhost", help="MQTT broker host")
    parser.add_argument("--port", type=int, default=1883, help="MQTT broker port")
    parser.add_argument("--device", default="device-001", help="Device ID")
    parser.add_argument("--duration", type=float, default=60.0, help="Duration in seconds")
    parser.add_argument("--rate", type=int, default=10, help="Sample rate in Hz")
    parser.add_argument("--noise", type=float, default=2.0, help="Noise standard deviation")
    parser.add_argument("--slope", type=float, default=0.1, help="Baseline slope")
    parser.add_argument("--mode", choices=["batch", "single"], default="batch",
                        help="Publish mode: batch (array of values) or single (one point per message)")
    parser.add_argument("--batch-size", type=int, default=50,
                        help="Points per batch message (batch mode only)")
    args = parser.parse_args()

    topic = f"instrument/{args.device}/signal"

    # Peak definitions: (retention_time, sigma, amplitude)
    peaks = [
        (5.0, 0.3, 100.0),
        (12.0, 0.4, 80.0),
        (20.0, 0.5, 120.0),
        (30.0, 0.6, 60.0),
    ]

    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id=f"publisher-{args.device}")
    client.user_data_set({"broker": args.broker, "port": args.port})
    client.on_connect = on_connect

    try:
        client.connect(args.broker, args.port, keepalive=60)
    except Exception as e:
        print(f"Failed to connect to broker: {e}", file=sys.stderr)
        sys.exit(1)

    client.loop_start()
    time.sleep(1.0)  # Wait for connection

    print(f"Publishing to topic: {topic}")
    print(f"Mode: {args.mode}, duration: {args.duration}s, rate: {args.rate}Hz")

    try:
        if args.mode == "single":
            for t, v in generate_chromatogram(args.duration, args.rate, args.noise, args.slope, peaks):
                payload = json.dumps({"t": round(t, 4), "v": round(v, 4)})
                client.publish(topic, payload, qos=1)
                time.sleep(1.0 / args.rate)
        else:
            # Batch mode
            points = list(generate_chromatogram(args.duration, args.rate, args.noise, args.slope, peaks))
            for i in range(0, len(points), args.batch_size):
                batch = points[i:i + args.batch_size]
                t0 = batch[0][0]
                values = [round(v, 4) for _, v in batch]
                payload = json.dumps({"rate": args.rate, "t0": round(t0, 4), "v": values})
                client.publish(topic, payload, qos=1)
                elapsed = len(batch) / args.rate
                time.sleep(elapsed)

        print("\nPublish complete.")
    except KeyboardInterrupt:
        print("\nInterrupted by user.")
    finally:
        client.loop_stop()
        client.disconnect()


if __name__ == "__main__":
    main()
