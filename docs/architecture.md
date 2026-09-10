# Architecture

> Technical architecture documentation for the **Mini-CDS** chromatography data system.
> Describes layered structure, data flow, data integrity controls (21 CFR Part 11 / ALCOA+),
> and cross-cutting concerns. All layer references are sourced from the actual source tree.

---

## 1. Layered Architecture

Mini-CDS follows **Clean Architecture** with a strict dependency direction: inner layers
know nothing about outer layers. Dependencies point inward.

```
┌─────────────────────────────────────────────────────────────┐
│  Presentation  (MiniCds.Wpf)                                │
│  Views, ViewModels, dialogs, converters                     │
└──────────────────┬──────────────────────────────────────────┘
                   │ depends on
┌──────────────────▼──────────────────────────────────────────┐
│  Infrastructure  (MiniCds.Infrastructure)                   │
│  EF Core repositories, SQLite, MQTT, PDF/CSV, signing       │
└──────────────────┬──────────────────────────────────────────┘
                   │ depends on
┌──────────────────▼──────────────────────────────────────────┐
│  Application  (MiniCds.Application)                          │
│  Acquisition service, DSP pipeline, audit/hash, auth        │
└──────────────────┬──────────────────────────────────────────┘
                   │ depends on
┌──────────────────▼──────────────────────────────────────────┐
│  Domain  (MiniCds.Domain)                                    │
│  Entities, ValueObjects, Enums, Abstractions (interfaces)   │
└─────────────────────────────────────────────────────────────┘
```

### Project references (verified from .csproj files)

| Project | Depends on | External packages |
|---|---|---|
| `MiniCds.Domain` | — | (none) |
| `MiniCds.Application` | Domain | `MathNet.Numerics` 5.0.0 |
| `MiniCds.Infrastructure` | Application, Domain | `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12, `QuestPDF` 2024.12.1, `MQTTnet` 4.3.6.1152 |
| `MiniCds.Wpf` | Infrastructure, Application, Domain | `Microsoft.Extensions.Hosting`, `Serilog.*`, WPF (`net10.0-windows`) |

---

## 2. Domain Layer (`MiniCds.Domain`)

No dependencies on EF, WPF, or any framework. Pure business contracts and types.

### Entities (`Entities/`)
| Entity | Purpose |
|---|---|
| `User` | System user (username, password hash/salt, role, active flag) |
| `Sample` | Chromatographic run (name, method, status, timestamps, audit) |
| `Method` | Acquisition/dsp method (name, `ProcessingParameters`) |
| `RawSignal` | Captured signal for a sample (sample rate, raw points as `byte[]`) |
| `Peak` | Detected peak entity (sample, apex/start/end indices, `PeakMetrics`) |
| `Report` | Generated report (title, format, file path, created by) |
| `ElectronicSignature` | 21 CFR Part 11 signature (meaning, reason, linked entity) |
| `AuditEntry` | Append-only audit record with SHA-256 hash chain |

### ValueObjects (`ValueObjects/`)
| Type | Description |
|---|---|
| `SignalFrame` | A single (timestamp, value) sample point |
| `PeakMetrics` | RetentionTime, Height, Area, WidthBase, Fwhm, Plates?, Tailing? |
| `ProcessingParameters` | DSP pipeline settings (MA window, Savitzky-Golay, baseline λ/p, peak thresholds) |

### Enums (`Enums/`)
`SampleStatus` (Queued → Running → Completed / Voided), `InstrumentState`, `UserRole`,
`SignatureMeaning` (Approved / Reviewed / Authorized / Rejected), `AuditAction`.

### Abstractions (`Abstractions/`)
All service and repository interfaces live here:
`IAcquisitionService`, `IInstrumentSource`, `ISignalProcessor`, `IReportService`,
`IReportExporter`, `ISignatureService`, `IAuditTrail`, `IHashChain`, `IPasswordHasher`,
`IUserStore`, and repositories (`ISampleRepository`, `IMethodRepository`,
`IRawSignalRepository`, `IPeakRepository`, `IReportRepository`).

---

## 3. Application Layer (`MiniCds.Application`)

Business orchestration and pure algorithms. No database, no UI.

### `Acquisition/AcquisitionService`
Orchestrates a chromatographic run:
1. Validates sample is in `Queued` state.
2. Subscribes to `IInstrumentSource.FrameReceived`.
3. Buffers `SignalFrame` points.
4. Every 10 frames runs the DSP pipeline in real time → raises `PeaksDetected`.
5. On stop: persists `RawSignal`, runs full DSP, persists `Peak` entities,
   transitions sample to `Completed`, audits all state changes.

### `SignalProcessing/` — DSP pipeline
`ISignalProcessor.Process(times, raw, parameters)` runs a stateless pure pipeline:

```
raw ──► MovingAverageFilter ──► SavitzkyGolayFilter ──► BaselineCorrector (ALS) ──► PeakDetector
                                                                                       │
                                                                                       ▼
                                                                              List<DetectedPeak>
```

- `MovingAverageFilter` — windowed mean smoothing.
- `SavitzkyGolayFilter` — polynomial smoothing + derivative.
- `BaselineCorrector` — Asymmetric Least Squares baseline fit (λ, p).
- `PeakDetector` — finds apexes, computes `PeakMetrics` (RT, height, area, FWHM,
  theoretical plates `N = 5.54·(RT/FWHM)²`, tailing factor).

### `Audit/`
- `HashChain` — SHA-256 hash chain. Each entry's hash covers its own fields plus the
  previous entry's hash; fields are length-prefixed (`len:value;`) to prevent
  boundary-shift collisions. Genesis entry uses sentinel `GENESIS`.
- `AuditService` — coordinates audit append + hash computation.

### `Auth/AuthService`
Username/password verification against `IUserStore`, returns an auth result with user id.

---

## 4. Infrastructure Layer (`MiniCds.Infrastructure`)

All I/O: database, instruments, file export, signing.

### Persistence (`Persistence/`)
- **`CdsDbContext`** — EF Core `DbContext` for SQLite. Exposes `DbSet`s for all 8
  entities. Registers `AppendOnlyInterceptor` via `OnConfiguring`.
- **`AppendOnlyInterceptor`** (`SaveChangesInterceptor`) — enforces append-only at the
  EF level:
  - `AuditEntry`, `ElectronicSignature`, `RawSignal`: block `Modified` and `Deleted`.
  - `Peak`, `Sample`: block `Deleted` (void instead).
- **`AuditTrail`** — `IAuditTrail` implementation. `AppendAsync` allocates IDs
  deterministically (`MAX(Id)+1`) inside a transaction because append-only triggers
  forbid post-insert `UPDATE`. `VerifyChainAsync` recomputes hashes and checks links.
  `QueryAsync` returns projection rows for the audit grid.
- **`SignatureService`** — 21 CFR Part 11 signing: re-authenticates (username +
  password must match the acting user), then atomically inserts `ElectronicSignature`
  and its linked `AuditEntry` in one transaction (cross-referencing IDs, no updates).
- **`EfUserStore`** — `IUserStore` backed by EF Core.
- **`DbSeeder`** — seeds a system user, an admin, a default `Method` and 3 demo
  samples on first run. Demo password comes from `Demo:Password`.
- **Repositories** — `SampleRepository`, `MethodRepository`, `RawSignalRepository`,
  `PeakRepository`, `ReportRepository`. All EF-based, parameterized queries.
- **Configurations** — per-entity `IEntityTypeConfiguration` (indexes, constraints,
  append-only triggers).

### Instruments (`Instruments/`)
- **`IInstrumentSource`** — contract for any signal producer:
  `FrameReceived` event, `StartAsync`/`StopAsync`, `InstrumentState`.
- **`SimulatorInstrumentSource`** — generates synthetic chromatograms (Gaussian peaks,
  Gaussian noise, linear baseline). Configurable sample rate, duration, peak list.
- **`MqttInstrumentSource`** — subscribes to `instrument/{deviceId}/signal` (QoS 1)
  and parses JSON frames. Supports both single-point (`{"t":1.2,"v":0.5}`) and batch
  (`{"rate":10,"t0":0,"v":[...]}`) payloads.

### Reporting (`Reporting/`)
- **`IReportExporter`** — `Format` string + `ExportAsync(samples, filePath)`.
- **`CsvReportExporter`** / **`PdfReportExporter`** (QuestPDF Community license).
- **`ReportService`** — selects exporter by format string (case-insensitive), builds
  the file path from the chosen output directory, persists a `Report` entity, and
  audits the generation.

### Security (`Security/`)
- **`PasswordHasher`** — PBKDF2-SHA256, 100,000 iterations, 16-byte random salt,
  32-byte key, constant-time comparison.

### DI Registration
`ServiceCollectionExtensions.AddMiniCdsPersistence(connectionString)` registers all
scoped services. `IInstrumentSource` is registered by the **WPF host** so it can switch
between Simulator and Mqtt based on configuration.

---

## 5. Presentation Layer (`MiniCds.Wpf`)

WPF MVVM desktop client (`net10.0-windows`, `UseWPF`).

### Composition root (`App.xaml.cs`)
- `.NET Generic Host` (`Host.CreateDefaultBuilder`) + Serilog.
- Loads `appsettings.json` (connection string, `Instrument:Mode`, `Demo:Password`).
- Registers persistence, then chooses `IInstrumentSource` (Simulator or Mqtt).
- One DI **scope per window** — `DbContext` lifetime matches the window lifetime.
- `ShutdownMode="OnExplicitShutdown"` — app exits explicitly after login/main window close.

### Startup flow
1. Host starts, `data/` folder created, EF migrations applied, DB seeded.
2. `LoginWindow` shown modally via `IUserStore`/`IAuthService`.
3. On success: `MainWindow` created with the authenticated `actorUserId`; the window
   scope is disposed and `Shutdown()` called when the main window closes.

### Views / ViewModels
| View | ViewModel | Purpose |
|---|---|---|
| `LoginWindow` | `LoginViewModel` | Username/password login |
| `MainWindow` | — (code-behind wiring) | Hosts `LiveChartView`, menu (File → Export Report, View → Audit Trail) |
| `LiveChartView` | `LiveChartViewModel` | Real-time chromatogram canvas, peaks grid, start/stop/void |
| `PeaksView` | — (ItemsControl bound to `DetectedPeaks`) | Detected peak table (RT, height, area, FWHM, plates, tailing) |
| `SignatureDialog` | `SignatureDialogViewModel` | 21 CFR Part 11 electronic signature (re-auth + meaning + reason) |
| `ReportDialog` | `ReportDialogViewModel` | Select samples, format (CSV/PDF), title |
| `AuditWindow` | — (code-behind) | Audit trail grid + "Verify Integrity" button |

### MVVM infrastructure
- `RelayCommand` / `AsyncRelayCommand` with `CanExecute` + `RaiseCanExecuteChanged`.
- `PasswordBox` is not bindable; password is synced from the `PasswordChanged` event
  into a set-only `Password` property on the VM.
- Dialogs needing runtime params (e.g. `actorUserId`) are created with
  `ActivatorUtilities.CreateInstance(_serviceProvider, actorUserId)`.

### Chart rendering (`LiveChartView.xaml.cs`)
- Custom-drawn `Polyline` on a `Canvas`. Axes and labels are drawn dynamically from
  `ActualWidth`/`ActualHeight` so the chart stays inside the `GroupBox` at any window
  size. Peak markers are dashed vertical lines at each detected apex.

---

## 6. Data Flow: Acquisition End-to-End

```
IInstrumentSource ──FrameReceived──► AcquisitionService
                                          │
                    ┌─────────────────────┤
                    │  (every 10 frames)  │
                    ▼                     ▼
         ISignalProcessor.DSP      frame buffer list
          (real-time peaks)              │
                    │                     │
                    ▼                     ▼
       LiveChartViewModel         On Stop:
        (UI live update)         • persist RawSignal
                                  • run full DSP
                                  • persist Peak entities
                                  • Sample → Completed
                                  • audit SampleStatusChanged + PeakDetected
```

- Instrument → service: pull-less event model (`FrameReceived`).
- Service → UI: `FrameProcessed` (per point) + `PeaksDetected` (periodically and on stop).
- All state transitions are recorded through `IAuditTrail`.

---

## 7. Data Integrity & 21 CFR Part 11 Controls

| Control | Implementation |
|---|---|
| **Append-only audit trail** | `AppendOnlyInterceptor` blocks Modify/Delete on `AuditEntry`, `ElectronicSignature`, `RawSignal`; Delete blocked on `Peak`/`Sample` (void instead). DB-level append-only triggers (`AddAppendOnlyTriggers` migration). |
| **Tamper-evident hash chain** | `HashChain` (SHA-256, length-prefixed fields); `IAuditTrail.VerifyChainAsync` recomputes the whole chain. |
| **Electronic signatures** | `SignatureService.SignAsync` requires re-authentication, records `SignatureMeaning` + reason, links to entity, creates audit entry atomically. |
| **Immutable raw data** | `RawSignal` cannot be modified or deleted once written. |
| **Audit of every state change** | Sample transitions (Queued→Running→Completed/Voided), peak detection, report generation, signing. |
| **Password storage** | PBKDF2-SHA256 (100k iters, random salt, constant-time verify). |
| **ALCOA+** | Attributable (actor user id), Legible, Contemporaneous (UTC timestamps), Original (append-only), Accurate; plus Complete, Consistent, Enduring, Available. |

### `AuditEntry` hash chain fields
`Id`, `TimestampUtc`, `ActorUserId`, `Action`, `EntityType`, `EntityId`, `Reason`,
`OldValues` (JSON), `NewValues` (JSON), `SignatureId?`, `PrevHash`, `Hash`.

---

## 8. Configuration (`appsettings.json`)

```json
{
  "ConnectionStrings": { "CdsDb": "Data Source=data/cds.db" },
  "Demo": { "Password": "demo123" },
  "Instrument": {
    "Mode": "Simulator",              // or "Mqtt"
    "Mqtt": {
      "BrokerHost": "localhost",
      "BrokerPort": 1883,
      "DeviceId": "device-001"
    }
  },
  "Serilog": { /* file sink, compact JSON, 10 MB roll */ }
}
```

- `Instrument:Mode` selects the `IInstrumentSource` implementation at startup.
- `Demo:Password` seeds the admin user's password on first run; changing it requires
  deleting `data/` to re-seed.

---

## 9. Cross-Cutting Concerns

### Logging
Serilog with `Serilog.Sinks.File`, compact JSON formatter, daily rolling, 10 MB file
size rollover. Configured via `appsettings.json`. EF Core logs are suppressed below
`Warning`.

### Error handling
- `DispatcherUnhandledException` in `App` catches UI-thread crashes, logs them, shows
  a dialog, and marks them handled.
- `OnStartup` wraps the async startup flow; failures surface via a dialog and shut
  the app down cleanly.

### Tests
xUnit + NSubstitute + FluentAssertions in `tests/MiniCds.Tests`. Coverage includes
`PasswordHasher`, `HashChain`, `AuditTrail` (verify chain), `SignatureService`,
`AcquisitionService`, DSP components, and repositories. AAA pattern.

---

## 10. Known Deviations (Notes for Future Work)

- **`ReportService` is in `Infrastructure`** rather than `Application`. The interface
  (`IReportService`) lives in Domain, but the concrete implementation sits in
  Infrastructure because it depends on exporters. This is a minor clean-architecture
  deviation; moving it to Application would require abstracting exporter selection.
- **Password hashing** uses PBKDF2-SHA256; production-grade deployments should switch
  to Argon2id (documented in `PasswordHasher` XML comments).
