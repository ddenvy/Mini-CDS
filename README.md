# Mini-CDS

Минимальная хроматографическая система сбора данных (CDS) с поддержкой требований **21 CFR Part 11** и принципов **ALCOA+** к целостности данных. Реализована на .NET 10 / WPF, использует SQLite (EF Core) для хранения данных и MQTTnet для интеграции с инструментами.

> Учебный/демонстрационный проект для лабораторного контекста. Не является сертифицированным ПО для регулируемого использования.

---

## Возможности

- **Acquisition в реальном времени** — потоковая обработка сигнала с инструмента (симулятор или MQTT).
- **Обработка сигнала** — базовая коррекция, сглаживание (Savitzky-Golay, Moving Average), детекция пиков и расчёт метрик (RT, Height, Area, FWHM, Plates, Tailing).
- **Live Chart** — отображение хроматограммы и таблицы детектированных пиков в реальном времени.
- **Electronic Signature (21 CFR Part 11 §11.50, §11.200)** — подписание действий (void sample) с проверкой имени пользователя и пароля, сохранением смысла подписи и причины.
- **Audit Trail (21 CFR Part 11 §11.10(e))** — неизменяемый журнал всех действий пользователей с hash-chain для проверки целостности.
- **Append-Only БД** — триггеры SQLite, запрещающие UPDATE/DELETE в критических таблицах.
- **Отчёты** — экспорт результатов в CSV и PDF (QuestPDF).
- **Аутентификация** — Argon2id хеширование паролей, сессии.
- **MQTT интеграция** — приём сигнала от внешнего инструмента через MQTT-брокер.

---

## Технологии

| Слой | Технологии |
|------|------------|
| UI | WPF (.NET 10), MVVM |
| Domain / Application | .NET 10, C# |
| ORM | EF Core 10, SQLite |
| MQTT | MQTTnet 4.3.6 |
| PDF | QuestPDF 2024.12 |
| Тесты | xUnit, NSubstitute, FluentAssertions |
| Логирование | Serilog |

---

## Архитектура

Проект следует **Clean Architecture** с разделением на слои:

```
MiniCds.Domain          — сущности, перечисления, value-объекты, интерфейсы (абстракции)
MiniCds.Application     — бизнес-логика (acquisition, audit, auth, signal processing)
MiniCds.Infrastructure  — реализация абстракций (EF Core, MQTT, reporting, security)
MiniCds.Wpf             — представление (MVVM, Views, ViewModels)
```

### Поток данных acquisition

```
IInstrumentSource (Simulator/MQTT)
    → SignalFrame (t, v)
    → IAcquisitionService
    → IRawSignalRepository (сырые данные)
    → ISignalProcessor (baseline, filter, peak detection)
    → IPeakRepository (метрики пиков)
    → LiveChartViewModel (UI)
```

---

## Структура проекта

```
src/
├── MiniCds.Domain/
│   ├── Abstractions/        # интерфейсы (IAcquisitionService, IAuditTrail, IInstrumentSource, ...)
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
│   ├── Persistence/         # EF Core, репозитории, AuditTrail, AppendOnlyInterceptor
│   ├── Reporting/           # CsvReportExporter, PdfReportExporter, ReportService
│   └── Security/            # PasswordHasher (Argon2id)
└── MiniCds.Wpf/
    ├── Views/               # MainWindow, LiveChartView, PeaksView, AuditWindow, ...
    ├── ViewModels/          # Login, LiveChart, ReportDialog, SignatureDialog
    └── Infrastructure/      # RelayCommand, AsyncRelayCommand
tools/
└── mqtt-publisher/          # Python publisher для тестирования MQTT
tests/MiniCds.Tests/         # xUnit тесты
```

---

## Быстрый старт

### Требования

- .NET 10 SDK
- Windows (WPF)
- (опционально) MQTT-брокер (mosquitto) и Python 3.8+ для работы с MQTT

### Сборка и запуск

```bash
dotnet build
dotnet run --project src/MiniCds.Wpf
```

### Учётные данные

По умолчанию создаётся демо-пользователь:

| Поле | Значение |
|------|----------|
| Логин | `admin` |
| Пароль | `demo123` |

Пароль задаётся в `appsettings.json` (`Demo:Password`). Если пароль был изменён, удалите папку `data/` для пересоздания БД.

### Запуск тестов

```bash
dotnet test
```

---

## Конфигурация

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

### Режимы инструмента

- **Simulator** (по умолчанию) — генерация гауссовых пиков с шумом и дрейфом базовой линии.
- **Mqtt** — приём сигнала от внешнего источника через MQTT-брокер.

---

## MQTT интеграция

### Топик

```
instrument/{deviceId}/signal
```

### Форматы сообщений (JSON)

**Одна точка:**
```json
{"t": 12.34, "v": 0.567}
```

**Пакет точек:**
```json
{"rate": 10, "t0": 0.0, "v": [0.11, 0.12, 0.14, ...]}
```

QoS — At Least Once (1), retain — false.

### Python publisher

Инструмент для эмуляции хроматографа:

```bash
cd tools/mqtt-publisher
pip install -r requirements.txt
python publish_chromatogram.py --broker localhost --port 1883 --device device-001
```

Опции: `--mode batch|single`, `--duration`, `--rate`, `--noise`, `--slope`, `--batch-size`.

---

## Соответствие нормативам

### 21 CFR Part 11

| Требование | Реализация |
|------------|------------|
| §11.10(d) Ограниченный доступ | Аутентификация (Argon2id), роли |
| §11.10(e) Audit trail | `IAuditTrail`, неизменяемый журнал, hash-chain |
| §11.10(g) Authority checks | Роли пользователей (Admin, Analyst) |
| §11.10(k) Append-only | Триггеры SQLite (нет UPDATE/DELETE) |
| §11.50 Электронные подписи | `ISignatureService`, проверка учётных данных |
| §11.200 Компоненты подписи | Username + Password + Meaning + Reason |

### ALCOA+

- **Attributable** — все действия привязаны к пользователю (actorUserId).
- **Legible** — читаемые записи в БД и отчётах.
- **Contemporaneous** — временные метки (TimestampUtc) на каждое событие.
- **Original** — append-only, нет изменений после записи.
- **Accurate** — проверка целостности hash-chain.
- **Complete, Consistent, Enduring, Available** — SQLite, резервные копии.

---

## Лицензия

Учебный проект. QuestPDF используется под Community-лицензией.
