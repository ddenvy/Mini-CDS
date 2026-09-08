# Mini-CDS — план реализации (пишет пользователь, .NET 10)

## Context

Директория `C:\Develop\Mini-CDS` пуста. Нужно с нуля построить desktop-демо хроматографической
data-системы (аналог Agilent OpenLAB CDS): приём телеметрии «прибора» → DSP-обработка в реальном
времени → live-график с пиками → таблица пиков → append-only аудит (21 CFR Part 11) → mock
электронная подпись → экспорт отчёта. Цель — портфолио-проект, поэтому упор на чистую слоистую
архитектуру, проверяемый DSP, реальную data-integrity и внятный README.

**Окружение (проверено):** .NET SDK 10.0.301 + WindowsDesktop runtime 10.0.9 (WPF работает),
git 2.54 (репо не инициализирован), Python 3.12 (для паблишера), winget есть, **mosquitto НЕ
установлен**. Отсюда решение: источник по умолчанию — встроенный симулятор; внешний MQTT закрыт
тремя путями (MQTTnet-клиент + Python-паблишер + опциональный встроенный брокер), чтобы DoD
«данные с внешнего MQTT» выполнялся без обязательной установки mosquitto.

**Роль плана:** код пишешь сам. Ниже — архитектура, дерево папок, контракты/формулы и пошаговый
порядок сборки с точками проверки. Сниппеты — это сигнатуры и формулы («как делать»), не готовая
реализация.

---

## 1. Технологические решения

| Область | Выбор | Замечания |
|---|---|---|
| Runtime | `net10.0` (библиотеки), `net10.0-windows` (WPF) | SDK 10.0.301 установлен |
| UI | WPF + MVVM | `CommunityToolkit.Mvvm` 8.x (`[ObservableProperty]`, `[RelayCommand]`, `ObservableObject`, `Messenger`) |
| График | ScottPlot 5 | пакет `ScottPlot.WPF`; для стриминга `Plot.Add.DataStreamer()`; рендер в PNG для PDF: `Plot.GetImage(w,h)` |
| DSP | свой код + `MathNet.Numerics` 5 | SG-коэффициенты и ALS-baseline считаем сами; MathNet — линейная алгебра |
| MQTT | `MQTTnet` (запинить мажор: 4.x — API ниже; в 5.x часть имён изменена) | клиент + опциональный `MqttServer` (встроенный брокер) |
| ORM | EF Core 10 + `Microsoft.EntityFrameworkCore.Sqlite` | + `Microsoft.EntityFrameworkCore.Design` для миграций |
| PDF | `QuestPDF` (Community license) | `QuestPDF.Settings.License = LicenseType.Community;` |
| CSV | `CsvHelper` или `StreamWriter` + `CultureInfo.InvariantCulture` | |
| DI/хост | `Microsoft.Extensions.Hosting` + `.DependencyInjection` | generic host в `App.xaml.cs` |
| Хеширование паролей | BCL `Rfc2898DeriveBytes` (PBKDF2-SHA256) | ≥100k итераций, уникальный salt на пользователя. ⚠️ Production → Argon2id (пакет `Argon2Sharp`, `Parallelism=2, MemorySize=65536, Iterations=3`), см. §8 |
| Валидация | `FluentValidation` 12 + `System.ComponentModel.DataAnnotations` | EF-сущности → DataAnnotations; application-level команды → FluentValidation pipeline |
| Тесты | xUnit 2.9 + `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio` + `NSubstitute` | `FluentAssertions` (обязательно для DSP/интеграционных) |

Общие свойства csproj: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`,
`<TreatWarningsAsErrors>` по желанию. Для WPF дополнительно `<UseWPF>true</UseWPF>`.

---

## 2. Слоистая архитектура

Направление зависимостей — строго внутрь (Domain ничего не знает о внешних слоях):

```
                 ┌──────────────────────────────┐
                 │        MiniCds.Wpf            │  composition root, UI/MVVM, ScottPlot
                 │   (net10.0-windows)           │
                 └───────────────┬───────────────┘
                                 │ refs
                 ┌───────────────▼───────────────┐
                 │     MiniCds.Infrastructure    │  EF Core+SQLite, MQTT/Simulator,
                 │        (net10.0)              │  CSV/PDF exporters, PBKDF2
                 └───────────────┬───────────────┘
                                 │ refs (реализует абстракции)
                 ┌───────────────▼───────────────┐
                 │      MiniCds.Application      │  AcquisitionService, SignalProcessor(DSP),
                 │        (net10.0)              │  AuditService, SignatureService, ReportService
                 └───────────────┬───────────────┘
                                 │ refs
                 ┌───────────────▼───────────────┐
                 │        MiniCds.Domain         │  сущности, enums, value objects, АБСТРАКЦИИ
                 │        (net10.0)              │  (IInstrumentSource, IAuditTrail, ...)
                 └───────────────────────────────┘

tests/MiniCds.Tests (net10.0) → refs Domain + Application + Infrastructure
```

Правило: **Application зависит только от Domain** и работает через интерфейсы; конкретика
(EF, MQTT, QuestPDF) живёт в Infrastructure и подставляется через DI в Wpf.

---

## 3. Дерево папок и файлов

```
Mini-CDS/
├─ MiniCds.sln
├─ .gitignore                      # стандартный dotnet gitignore (bin/obj/*.db/*.user)
├─ README.md                       # lab-контекст, ALCOA+, Part 11, как запустить, GIF
├─ docs/architecture.md            # (опц.) схема + описание потоков
├─ data/                           # (gitignore) cds.db — либо %LocalAppData%\MiniCds\
│
├─ src/
│  ├─ MiniCds.Domain/
│  │  ├─ Entities/                 # Sample, Method, RawSignal, Peak, AuditEntry, ElectronicSignature, User
│  │  ├─ Enums/                    # SampleStatus, AuditAction, SignatureMeaning, InstrumentState, UserRole
│  │  ├─ ValueObjects/             # SignalFrame, PeakMetrics, ProcessingParameters
│  │  └─ Abstractions/             # IInstrumentSource, ISignalProcessor, IAuditTrail,
│  │                               # ISignatureService, IReportExporter, IPasswordHasher, IHashChain
│  │
│  ├─ MiniCds.Application/
│  │  ├─ Acquisition/              # DOD: structs + lookup tables + flat loops (AGENT §2.4)
│  │  │                            # AcquisitionService, SignalBuffer (rolling), Channel-pipeline
│  │  ├─ SignalProcessing/         # DOD: structs + lookup tables + flat loops (AGENT §2.4)
│  │  │                            # SignalProcessor (оркестратор), MovingAverageFilter,
│  │  │                            # SavitzkyGolayFilter, BaselineCorrector, PeakDetector
│  │  ├─ Audit/                    # AuditService, HashChain (SHA256-цепочка)
│  │  ├─ Security/                 # SignatureService (логика mock-подписи)
│  │  └─ Reporting/                # ReportService (сбор данных для CSV/PDF)
│  │
│  ├─ MiniCds.Infrastructure/
│  │  ├─ Persistence/              # CdsDbContext, Configurations/*, AppendOnlyInterceptor,
│  │  │   └─ Migrations/           # DesignTimeDbContextFactory
│  │  ├─ Instruments/              # SimulatorInstrumentSource, MqttInstrumentSource, EmbeddedMqttBroker
│  │  ├─ Reporting/                # CsvReportExporter, PdfReportExporter(QuestPDF)
│  │  └─ Security/                 # PasswordHasher (PBKDF2)
│  │
│  └─ MiniCds.Wpf/
│     ├─ App.xaml / App.xaml.cs    # bootstrap generic host + DI
│     ├─ appsettings.json          # Instrument:{Mode,Mqtt{Host,Port,Topic}}, ConnectionString
│     ├─ DI/HostBuilderExtensions.cs
│     ├─ Services/UiDispatcher.cs  # маршалинг в UI-поток + throttle
│     ├─ ViewModels/               # MainViewModel, LiveChartViewModel, PeaksViewModel,
│     │                            # AuditViewModel, SignatureDialogViewModel, LoginViewModel
│     ├─ Views/                    # MainWindow, LiveChartView(WpfPlot), PeaksView(DataGrid),
│     │                            # AuditView, SignatureDialog, LoginWindow
│     └─ Converters/               # enum→brush (статус прибора), bool→visibility и т.п.
│
├─ tests/MiniCds.Tests/
│  ├─ SignalProcessing/            # MovingAverageTests, SavitzkyGolayTests,
│  │                               # BaselineCorrectorTests, PeakDetectorTests
│  ├─ Audit/                       # AuditServiceTests, AppendOnlyTests, HashChainTamperTests
│  ├─ Persistence/                 # in-memory/temp-file SQLite
│  └─ Helpers/TestSignalGenerator.cs  # сумма Гауссов + шум
│
└─ tools/mqtt-publisher/
   ├─ publish_chromatogram.py      # paho-mqtt, генерирует и публикует точки в реальном времени
   ├─ requirements.txt             # paho-mqtt
   └─ README.md                    # как запустить, топик/формат
```

---

## 4. Доменная модель (ключевые сущности)

- **Method** — параметры съёмки/обработки (версионируемые): `SampleRateHz`, `MovingAverageWindow`,
  `SgWindow`, `SgOrder`, `BaselineLambda`, `BaselineP`, `MinPeakHeight`, `MinProminence`,
  `MinWidthPoints`. Изменение метода → audit-запись (старый→новый).
- **Sample** — `Id, Name, MethodId, Status (Queued|Running|Completed|Voided), CreatedAtUtc,
  CreatedByUserId, VoidedAtUtc?, VoidedReason?`. Void вместо удаления.
- **RawSignal** (immutable) — `Id, SampleId, CapturedAtUtc, SampleRateHz, Points(byte[] или JSON)`.
  «Original»-копия для ALCOA+.
- **Peak** (immutable) — `Id, SampleId, ApexIndex, StartIndex, EndIndex, RetentionTime, Height,
  Area, WidthBase, WidthHalfHeight, Plates?, Tailing?, DetectedAtUtc, IsManual`. Ручная
  ре-интеграция создаёт НОВУю версию пика + audit, старая помечается voided.
- **AuditEntry** (append-only) — `Id(long, возрастающий), TimestampUtc, ActorUserId, Action(enum),
  EntityType, EntityId, Reason?, OldValues(json)?, NewValues(json)?, SignatureId?, PrevHash, Hash`.
- **ElectronicSignature** (immutable) — `Id, UserId, Meaning(Approved|Reviewed|...), Reason,
  SignedAtUtc, LinkedEntityType, LinkedEntityId, AuditEntryId`.
- **User** — `Id, Username, FullName, PasswordHash, PasswordSalt, Role, IsActive, CreatedAtUtc`.

### Абстракции (Domain/Abstractions)

```csharp
public interface IInstrumentSource {
    InstrumentState State { get; }
    event EventHandler<SignalFrame>? FrameReceived;      // или IAsyncEnumerable<SignalFrame>
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}
public readonly record struct SignalFrame(double TimestampSeconds, double Value);

public interface ISignalProcessor {
    ProcessedSignal Process(ReadOnlySpan<double> times, ReadOnlySpan<double> raw, ProcessingParameters p);
}
// ProcessedSignal: Smoothed[], Baseline[], Corrected[], Peaks(IReadOnlyList<Peak>)

public interface IAuditTrail {
    Task<long> AppendAsync(AuditAction action, string entityType, long entityId,
        string? reason, object? oldValues, object? newValues, long actorUserId, long? signatureId);
    Task<bool> VerifyChainAsync(CancellationToken ct);   // пересчёт SHA256-цепочки
    Task<IReadOnlyList<AuditEntry>> QueryAsync(...);
}
public interface ISignatureService { Task<ElectronicSignature?> SignAsync(string user, string pwd, SignatureMeaning meaning, string reason, string entityType, long entityId); }
public interface IReportExporter  { Task ExportAsync(SampleReport model, string path, ReportFormat fmt); }
public interface IPasswordHasher  { (string hash,string salt) Hash(string pwd); bool Verify(string pwd,string hash,string salt); }
```

---

## 5. DSP-конвейер (ядро — делать ПЕРВЫМ, через TDD)

Порядок: `raw → moving average → Savitzky–Golay → baseline correction → peak detection`.

### 5.1 Moving average
`out[i] = mean(raw[i-m .. i+m])`, окно `w=2m+1`. На краях — уменьшать окно (или отражать).
Тест: константа на входе → та же константа; известный короткий массив → ожидаемый.

### 5.2 Savitzky–Golay
Окно `2m+1`, полином порядка `p (p<2m+1)`. Коэффициенты свёртки `c[-m..m]` — из псевдообратной
матрицы Вандермонда:
- строки `J[k] = [1, k, k², …, kᵖ]`, `k = -m..m`;
- `C = (JᵀJ)⁻¹ Jᵀ`; сглаживающие коэффициенты = строка 0 `C`; нормировка `Σc = 1`.
- Первая производная = строка 1 `C`, делённая на `dt` → пригодится для peak detection.

`smoothed[i] = Σ_k c[k]·signal[i+k]`. Считать коэффициенты ОДИН раз (кэш по (m,p)).
**Сильный тест:** SG сохраняет полином степени ≤ p точно → подать квадратичную функцию
(окно 5, порядок 2) → выход == вход (в пределах 1e-9).

### 5.3 Baseline correction
Рекомендуемый метод — **ALS (Asymmetric Least Squares / Whittaker)**, стандарт для хроматографии:
минимизировать `Σ w_i(y_i − z_i)² + λ·Σ(Δ²z)²`, где `w_i = p если y_i>z_i иначе (1−p)`;
10–20 итераций; `λ ≈ 1e5…1e7`, `p ≈ 0.001…0.01`. Решать разреженную ленточную СЛАУ
(MathNet sparse или ленточный Холецки). `corrected = y − z`.
Простой запасной вариант: baseline = скользящий минимум по большому окну (≈3× ширина пика)
→ сгладить → вычесть.
Тест: сигнал = линейный/выгнутый baseline + Гауссовы пики → в беспиковых зонах `corrected ≈ 0`,
пики сохранены.

### 5.4 Peak detection
1. Взять `corrected` (уже сглаженный). Производная `dy` (SG-коэф. 1-го порядка или центральная разность).
2. Локальные максимумы: смена знака `dy` с `+` на `−` между `i` и `i+1` → апекс ≈ `i`.
3. Фильтры: `height ≥ MinPeakHeight`, `prominence ≥ MinProminence`, ширина в `[MinWidth, MaxWidth]`.
   Prominence = высота апекса над ближайшими седловинами (минимум между соседними апексами).
4. Границы пика: от апекса идти влево/вправо, пока `y` не упадёт до baseline (≈0) или не достигнет
   долины/старта соседнего пика → `StartIndex/EndIndex`.
5. Метрики:
   - **RT** = `times[apex]`.
   - **Height** = `y[apex]` (после baseline).
   - **Area** = трапеция `Σ (y[i]+y[i+1])/2·dt` на `[start..end]`.
   - **WidthBase** = `times[end] − times[start]`; **FWHM** — полувысотные пересечения с интерполяцией.
   - (бонус, показывает домен) **Plates** `N = 5.54·(RT/FWHM)²`; **Tailing** `= W0.05/(2·f)`.

### 5.5 Тестовый генератор сигнала
`f(t) = Σ A_j·exp(−(t−t0_j)²/(2σ_j²)) + N(0, σ_noise)`.
Аналитика для проверок: площадь Гаусса `= A·σ·√(2π)`, FWHM `= 2.3548·σ`.
Тесты: 1 Гаусс → 1 пик (RT≈t0±dt, Height≈A±tol, Area≈A·σ·√(2π)±tol); смесь 2 Гауссов → 2 пика с
верными RT; чистый шум (с порогами) → 0 пиков.

---

## 6. Instrument layer + Acquisition

- **SimulatorInstrumentSource** — генерирует хроматограмму (5.5) в реальном времени с заданным
  `SampleRateHz`, публикует `SignalFrame` в Channel. Источник по умолчанию для демо/GIF.
- **MqttInstrumentSource** — подключается к брокеру, подписывается на топик, парсит payload →
  `SignalFrame` → тот же Channel. Контракт в разделе 9.
- **EmbeddedMqttBroker** (опц.) — `MqttFactory().CreateMqttServer(...)` на `localhost:1883`, чтобы
  гонять MQTT end-to-end без mosquitto.
- **AcquisitionService** — продюсер/консьюмер через `System.Threading.Channels`
  (`Channel<SignalFrame>`): инструмент пишет, сервис читает в фоне, копит в `SignalBuffer`
  (rolling), раз в N точек / T мс прогоняет окно через `ISignalProcessor`, обновляет пики,
  батчами пишет `RawSignal` (не по одной точке!), поднимает события для UI. Всё вне UI-потока.
- `SignalBuffer` — кольцевой буфер на `double[]`/`ConcurrentQueue` с окном прокрутки.

Состояния прибора (`InstrumentState`): `Idle|Connecting|Streaming|Stopping|Faulted` → биндятся в
статус-бар UI.

---

## 7. Persistence (EF Core + SQLite) + append-only

- **CdsDbContext** с `DbSet<>` для всех сущностей; конфигурации через `IEntityTypeConfiguration`
  (индексы, precision, связи, `HasQueryFilter` для voided где уместно).
- Connection string в `appsettings.json`; файл БД — `data/cds.db` рядом с exe или
  `%LocalAppData%\MiniCds\cds.db` (DoD «переживает перезапуск»).
- Миграции: `dotnet ef migrations add Init -p src/MiniCds.Infrastructure -s src/MiniCds.Wpf`
  (+ `DesignTimeDbContextFactory` для design-time).

### N+1-запросы (user rules: Always check for N+1 queries)

- Связанные сущности — `.Include().ThenInclude()` в **одном** запросе. Никаких `ToList()` внутри цикла.
- Read-only (UI-отрисовка, чтение отчётов) — `.AsNoTracking()`.
- Пакетная запись (Sample + RawSignal + Peak батчами) — `.AddRange()` / `.UpdateRange()`. Не по одной сущности.

### Валидация

- EF-сущности — `[Required]`, `[MaxLength]`, `[Range]` из `System.ComponentModel.DataAnnotations`.
- Application-level команды (стартовый вход, старт съёмки, ре-интеграция) — `FluentValidation` pipeline.

### Append-only (3 уровня защиты — важно для DoD)
1. **EF-interceptor** `AppendOnlyInterceptor : SaveChangesInterceptor` — бросает исключение при
   `EntityState.Deleted` или `Modified` для immutable-сущностей (`AuditEntry`, `ElectronicSignature`,
   `Peak`, `RawSignal`).
2. **SQLite-триггеры** (в миграции через `migrationBuilder.Sql`) — физический запрет на уровне БД:
   ```sql
   CREATE TRIGGER trg_audit_no_update BEFORE UPDATE ON AuditTrail
     BEGIN SELECT RAISE(ABORT,'AuditTrail is append-only'); END;
   CREATE TRIGGER trg_audit_no_delete BEFORE DELETE ON AuditTrail
     BEGIN SELECT RAISE(ABORT,'AuditTrail is append-only'); END;
   ```
3. **Hash-chain** (tamper-evidence): `Hash_i = SHA256(Id|TimestampUtc|Action|ActorUserId|EntityType|
   EntityId|Reason|OldValues|NewValues|Hash_{i-1})`, genesis `PrevHash` = фикс. sentinel.
   `VerifyChainAsync()` пересчитывает и сверяет → любая подмена значения рвёт цепочку.

«Удаление» = `Voided`-флаг + `VoidReason` + `VoidedBy` + `VoidedAt` + НОВАЯ audit-запись.

### AuditService
Пишет `AuditEntry` на каждое значимое действие: создание/изменение метода, старт/стоп съёмки,
ручная ре-интеграция пика, void образца, экспорт отчёта, вход/подпись. Заполняет old→new (JSON-дамп
изменённых полей), проставляет `ActorUserId`, `SignatureId` (если действие подписано).

---

## 8. Электронная подпись (mock) и безопасность

- **PasswordHasher** — `Rfc2898DeriveBytes.Pbkdf2(pwd, salt, iterations≥100_000, SHA256, 32)`;
  хранить `salt`+`hash` base64. Сидинг демо-пользователей при первом старте (напр. `operator/analyst`).

  ⚠️ **Production hardening note** (user rules: Argon2 / BCrypt only): PBKDF2-SHA256 с ≥100k
  итераций и уникальным salt на пользователя — адекватная защита для портфолио-демо. Для
  production заменить на **Argon2id** (пакет `Argon2Sharp`):
  `Argon2.Hash(pwd, salt, parallelism: 2, memorySize: 65536, iterations: 3)` — memory-hard
  функция устойчива к ASIC/GPU-атакам, в отличие от PBKDF2.
- **SignatureService.SignAsync** — критичное действие требует ПОВТОРНОГО ввода логина+пароля:
  1. UI открывает `SignatureDialog` (модалка): Username, PasswordBox, Meaning(Approved|Reviewed),
     обязательный Reason.
  2. `Verify` пароля через `IPasswordHasher`; при неудаче — отмена (опц. audit неудачной попытки).
  3. При успехе — создать `ElectronicSignature` (timestamp UtcNow, привязка `LinkedEntityType/EntityId`)
     + `AuditEntry` с `SignatureId`, затем выполнить само действие.
- Критичные действия, требующие подписи: approve/void образца, смена метода, ручная ре-интеграция.
- `PasswordBox.Password` не биндится — передавать через code-behind/attached property.

---

## 9. MQTT: топик и payload-контракт + Python-паблишер

- Топик: `instrument/{deviceId}/signal`.
- Payload (JSON), два варианта:
  - по одной точке: `{"t": 12.34, "v": 0.567}`
  - батчем: `{"rate": 10, "t0": 0.0, "v": [0.11, 0.12, ...]}`
- QoS 1, retain=false. `MqttInstrumentSource` парсит оба варианта.
- **tools/mqtt-publisher/publish_chromatogram.py**: `paho-mqtt`; цикл — генерирует точку
  хроматограммы (Гауссы+шум, как в 5.5) и публикует раз в `1/rate` сек, имитируя прибор в реальном
  времени. `requirements.txt`: `paho-mqtt`.
- Запуск внешнего MQTT (варианты для DoD):
  1. Включить `EmbeddedMqttBroker` в приложении → запустить Python-паблишер на `localhost:1883`.
  2. Или поставить mosquitto: `winget install EclipseFoundation.Mosquitto` (проверить точный id) и
     поднять брокер отдельно.
  3. `appsettings.json`: переключить `Instrument:Mode` с `Simulator` на `Mqtt`.

---

## 10. WPF UI / MVVM

- **App.xaml.cs** — generic host: регистрация DbContext, сервисов, VM, окон; `App.Services`.
  Опц. `LoginWindow` на старте (кто оператор → `ActorUserId` для аудита).
- **MainViewModel** — статус прибора, команды Start/Stop, Export CSV/PDF, текущие Sample/Method,
  вложенные VM (LiveChart, Peaks, Audit).
- **LiveChartViewModel + LiveChartView (WpfPlot)** — 2 трейса (raw + processed) через
  `DataStreamer`, маркеры пиков (`Plot.Add.Marker`/`Scatter` + подписи RT).
  **Отзывчивость без подвисаний:** данные приходят из фонового потока → маршалинг через
  `UiDispatcher`; НЕ перерисовывать на каждую точку — `DispatcherTimer` ~20–30 FPS тянет свежий
  снапшот буфера и вызывает `WpfPlot.Refresh()`. Ось X — скользящее окно (rolling).
- **PeaksView** — `DataGrid` (RT, Height, Area, WidthBase, FWHM, Plates),
  `ObservableCollection<PeakRowViewModel>`; команда «Re-integrate» (требует подпись).
- **AuditView** — read-only `DataGrid` журнала (кто/когда/что/почему/old→new), фильтр по образцу и
  действию; команда «Verify integrity» → `VerifyChainAsync()`.
- Все мутации `ObservableCollection` — только в UI-потоке (через `Dispatcher`/`IProgress<T>`).

---

## 11. Экспорт отчётов

- **ReportService** собирает `SampleReport` (метаданные образца + метод + peaks + audit + signatures +
  PNG хроматограммы из ScottPlot).
- **CsvReportExporter** — секции: шапка образца, таблица пиков, audit-строки; `InvariantCulture`.
- **PdfReportExporter (QuestPDF)** — разделы: Sample info → Method → изображение хроматограммы →
  Peaks table → Audit trail → Signatures. Лицензия Community.

---

## 12. Тесты (xUnit)

- **DSP** (см. 5.x): moving average, SG-точная-на-полиноме, baseline, PeakDetector на
  известных Гауссах (RT/Height/Area/FWHM в допуске), шум → 0 пиков.
- **Audit/append-only**: append → `VerifyChain` ok; попытка Update/Delete через `DbContext` →
  interceptor бросает; прямая подмена значения в БД → `VerifyChain` падает; void → флаги + новая
  запись, оригинал на месте.
- **Persistence**: temp-file или `DataSource=:memory:` SQLite (держать соединение открытым).

---

## 13. Порядок сборки (milestones) + точки проверки

1. **Каркас**: `git init`, `.gitignore`, `dotnet new sln`, проекты (classlib×3, wpf, xunit),
   `dotnet sln add`, project references, NuGet-пакеты. → `dotnet build` зелёный.
2. **Domain**: сущности, enums, value objects, абстракции. → build.
3. **DSP + тесты (TDD)**: фильтры, BaselineCorrector, PeakDetector, TestSignalGenerator.
   → `dotnet test` зелёный. *(ядро — делаем до UI)*
4. **Persistence + Audit**: DbContext, конфигурации, миграция, AppendOnlyInterceptor, триггеры,
   HashChain, AuditService + тесты append-only. → `dotnet test` зелёный.
5. **Instruments + Acquisition**: SimulatorInstrumentSource, Channel-pipeline, AcquisitionService
   (simulator→processor→persist). → прогон в консоли/тесте.
6. **Security**: PasswordHasher, сидинг пользователей, SignatureService.
7. **WPF shell + live**: DI-хост, MainWindow, LiveChart (стрим с симулятора, throttled), Peaks grid,
   Audit view. → **проверить отзывчивость UI** (нет фризов при стриме).
8. **MQTT end-to-end**: MqttInstrumentSource + Python-паблишер + EmbeddedMqttBroker; переключить
   режим на Mqtt. → данные идут с внешнего источника.
9. **Экспорт**: CSV + PDF. → файлы открываются, содержат peaks+audit.
10. **E-signature dialog** на критичные действия. → подпись сохраняется с timestamp+привязкой.
11. **Персистентность**: перезапуск приложения → данные/аудит на месте.
12. **README + GIF** (ScreenToGif), финальная сверка DoD, `git commit`.

---

## 14. Definition of Done → где закрыто

| DoD | Где |
|---|---|
| Данные с внешнего MQTT (mosquitto/паблишер) | §6 MqttInstrumentSource + §9 Python-паблишер/EmbeddedMqttBroker |
| Переживают перезапуск | §7 EF Core+SQLite (`data/cds.db`), milestone 11 |
| Любое изменение → запись; удаление невозможно, только voided | §7 AuditService + AppendOnlyInterceptor + SQLite-триггеры + §7 Void |
| Живой график с маркерами пиков, UI отзывчивый | §10 LiveChart (DataStreamer + DispatcherTimer throttle) |
| Peak detection работает + юнит-тесты | §5.4 + §12 DSP-тесты |
| Append-only: физически удалить/перезаписать нельзя | §7 (3 уровня) + §12 AppendOnlyTests |
| Mock e-signature c timestamp + привязкой | §8 SignatureService/ElectronicSignature + SignatureDialog |
| Экспорт отчёта CSV/PDF | §11 |
| README: lab-контекст (data integrity, ALCOA+, Part 11) + GIF | §15 |

**ALCOA+ → реализация** (для README): Attributable→ActorUserId+подпись; Legible→UI/отчёты;
Contemporaneous→TimestampUtc; Original→immutable RawSignal; Accurate→валидированный DSP+тесты;
Complete→hash-chain без разрывов; Consistent→UTC+упорядоченная цепочка; Enduring→SQLite;
Available→запросы к аудиту+экспорт.
**21 CFR Part 11**: §11.10(e) audit trail, §11.50/11.70 e-signatures, неизменяемость записей.

---

## 15. README (структура)

1. Что это и зачем (аналог CDS, lab-контекст).
2. GIF-демо + скриншоты.
3. Архитектура (схема слоёв, поток данных).
4. Data integrity: ALCOA+, 21 CFR Part 11 — как закрыто (audit, e-signature, append-only, hash-chain).
5. Как запустить: симулятор (по умолчанию) и внешний MQTT (паблишер/брокер/mosquitto).
6. Как гонять тесты (`dotnet test`).
7. Структура решения (дерево).
8. Стек и версии.
9. DoD-чеклист.

---

## 16. Проверка (verification)

- Сборка/тесты: `dotnet build`, `dotnet test` (все зелёные).
- Прогон: `dotnet run --project src/MiniCds.Wpf` → Start (Simulator) → live-график с пиками →
  Peaks grid заполняется → критичное действие → SignatureDialog → Audit показывает запись →
  Export CSV/PDF → закрыть/открыть заново → данные на месте.
- Внешний MQTT: поднять EmbeddedMqttBroker (или mosquitto) → `python tools/mqtt-publisher/
  publish_chromatogram.py` → `Instrument:Mode=Mqtt` → стрим идёт с внешнего источника.
- Append-only: попытка удалить строку AuditTrail в БД → блокируется; `Verify integrity` в UI → ok;
  после ручной подмены значения → verify падает.
- GIF: ScreenToGif поверх окна во время стрима.

---

## Зафиксированные решения (подтверждены)

1. **Структура** → 5 проектов: `MiniCds.Domain`, `MiniCds.Application`, `MiniCds.Infrastructure`,
   `MiniCds.Wpf`, `MiniCds.Tests` (строгая слоистость, см. §2–3).
2. **Baseline correction** → **ALS (Whittaker)** как основной метод (§5.3). Разреженная СЛАУ через
   MathNet sparse или ленточный Холецки. Rolling-minimum — НЕ реализуем (оставлен только как
   справка-альтернатива, в код не идёт).
3. **Экспорт отчётов** → **CSV + PDF** (QuestPDF, Community license) — обе реализации (§11).
4. **Стартовый вход** → **LoginWindow на старте**: даёт `ActorUserId` для всех audit-записей
   (ALCOA+ Attributable); mock-подпись в SignatureDialog — поверх, для критичных действий (§8, §10).

Эти выборы отражены в milestone-плане (§13) и дереве папок (§3).