# Mini-CDS

A minimal chromatography data system (CDS) with **21 CFR Part 11** and **ALCOA+** data integrity support. Built on .NET 10 / WPF, using SQLite (EF Core) for storage and MQTTnet for instrument integration.

> Educational / demonstration project for a laboratory context. Not certified software for regulated use.

---

## Features

- **Real-time acquisition** — streaming signal processing from an instrument (simulator or MQTT).
- **Signal processing** — baseline correction, smoothing (Savitzky-Golay, Moving Average), peak detection, and metric calculation (RT, Height, Area, FWHM, Plates, Tailing).
- **Live Chart** — real-time chromatogram display and detected peaks table.
- **Electronic Signature (21 CFR Part 11 §11.50, §11.200)** — signing actions (e.g. void sample) with username/password verification, signature meaning, and reason.
- **Audit Trail (21 CFR Part 11 §11.10(e))** — immutable log of all user actions with hash-chain integrity verification.
- **Append-Only DB** — SQLite triggers preventing UPDATE/DELETE on critical tables.
- **Reporting** — export results to CSV and PDF (QuestPDF).
- **Authentication** — Argon2id password hashing, sessions.
- **MQTT integration** — receive signal from an external instrument via an MQTT broker.

---

## Tech Stack

| Layer | Technologies |
|-------|--------------|
| UI | WPF (.NET 10), MVVM |
| Domain / Application | .NET 10, C# |
| ORM | EF Core 10, SQLite |
| MQTT | MQTTnet 4.3.6 |
| PDF | QuestPDF 2024.12 |
| Testing | xUnit, NSubstitute, FluentAssertions |
| Logging | Serilog |

---

## Architecture

The project follows **Clean Architecture** with separated layers:

```
MiniCds.Domain          — entities, enums, value objects, interfaces (abstractions)
MiniCds.Application     — business logic (acquisition, audit, auth, signal processing)
MiniCds.Infrastructure  — abstraction implementations (EF Core, MQTT, reporting, security)
MiniCds.Wpf             — presentation (MVVM, Views, ViewModels)
```

### Acquisition data flow

```
IInstrumentSource (Simulator/MQTT)
    → SignalFrame (t, v)
    → IAcquisitionService
    → IRawSignalRepository (raw data)
    → ISignalProcessor (baseline, filter, peak detection)
    → IPeakRepository (peak metrics)
    → LiveChartViewModel (UI)
```

---

## Project Structure

```
src/
├── MiniCds.Domain/
│   ├── Abstractions/        # interfaces (IAcquisitionService, IAuditTrail, IInstrumentSource, ...)
│   ├── Entities/            # AuditEntry, ElectronicSignature, Sample, Peak, ...
│   ├── Enums/               # SampleStatus, AuditAction, SignatureMeaning, ...
│   └── ValueObjects/        # SignalFrame, PeakMetrics, ProcessingParameters
├── MiniCds.Application/
│   ├── Acquisition/         # AcquisitionService
│   ├── Audit/               # AuditService, HashChain
│   ├── Auth/                # AuthService
│   └── SignalProcessing/    # BaselineCorrector, Filters, PeakDetector, SignalProcessor
├── MiniCds.Infrastructure/
│   ├── Instruments/         # SimulatorInstrumentSource, MqttInstrumentSource
│   ├── Persistence/         # EF Core, repositories, AuditTrail, AppendOnlyInterceptor
│   ├── Reporting/           # CsvReportExporter, PdfReportExporter, ReportService
│   └── Security/            # PasswordHasher (Argon2id)
└── MiniCds.Wpf/
    ├── Views/               # MainWindow, LiveChartView, PeaksView, AuditWindow, ...
    ├── ViewModels/          # Login, LiveChart, ReportDialog, SignatureDialog
    └── Infrastructure/      # RelayCommand, AsyncRelayCommand
tools/
└── mqtt-publisher/          # Python publisher for MQTT testing
tests/MiniCds.Tests/         # xUnit tests
```

---

## Quick Start

### Requirements

- .NET 10 SDK
- Windows (WPF)
- (optional) MQTT broker (mosquitto) and Python 3.8+ for MQTT mode

### Build & Run

```bash
dotnet build
dotnet run --project src/MiniCds.Wpf
```

### Credentials

A demo user is created by default:

| Field | Value |
|-------|-------|
| Login | `admin` |
| Password | `demo123` |

The password is set in `appsettings.json` (`Demo:Password`). If the password was changed, delete the `data/` folder to recreate the DB.

### Run Tests

```bash
dotnet test
```

---

## Configuration

`src/MiniCds.Wpf/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "CdsDb": "Data Source=data/cds.db"
  },
  "Demo": {
    "Password": "demo123"
  },
  "Instrument": {
    "Mode": "Simulator",
    "Mqtt": {
      "BrokerHost": "localhost",
      "BrokerPort": 1883,
      "DeviceId": "device-001"
    }
  }
}
```

### Instrument Modes

- **Simulator** (default) — generates Gaussian peaks with noise and baseline drift.
- **Mqtt** — receives signal from an external source via an MQTT broker.

---

## MQTT Integration

### Topic

```
instrument/{deviceId}/signal
```

### Message Formats (JSON)

**Single point:**
```json
{"t": 12.34, "v": 0.567}
```

**Batch of points:**
```json
{"rate": 10, "t0": 0.0, "v": [0.11, 0.12, 0.14, ...]}
```

QoS — At Least Once (1), retain — false.

### Python Publisher

A chromatograph emulation tool:

```bash
cd tools/mqtt-publisher
pip install -r requirements.txt
python publish_chromatogram.py --broker localhost --port 1883 --device device-001
```

Options: `--mode batch|single`, `--duration`, `--rate`, `--noise`, `--slope`, `--batch-size`.

---

## Regulatory Compliance

### 21 CFR Part 11

| Requirement | Implementation |
|-------------|----------------|
| §11.10(d) Limited system access | Authentication (Argon2id), roles |
| §11.10(e) Audit trail | `IAuditTrail`, immutable log, hash-chain |
| §11.10(g) Authority checks | User roles (Admin, Analyst) |
| §11.10(k) Append-only | SQLite triggers (no UPDATE/DELETE) |
| §11.50 Electronic signatures | `ISignatureService`, credential verification |
| §11.200 Signature components | Username + Password + Meaning + Reason |

### ALCOA+

- **Attributable** — every action is linked to a user (actorUserId).
- **Legible** — readable records in the DB and reports.
- **Contemporaneous** — timestamps (TimestampUtc) on every event.
- **Original** — append-only, no modifications after recording.
- **Accurate** — hash-chain integrity verification.
- **Complete, Consistent, Enduring, Available** — SQLite, backups.

---

## License

Educational project. QuestPDF is used under the Community license.
