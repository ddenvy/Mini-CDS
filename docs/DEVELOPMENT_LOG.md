# Mini-CDS — Дневник разработки

## О проекте

Mini-CDS — компактная CDS-система (Chromatography Data System) для портфолио-проекта.
Платформа: .NET 10, WPF, SQLite. Архитектура: Clean/Layered (Domain → Application → Infrastructure),
Data-Oriented Design на hot paths (DSP), 21 CFR Part 11 — append-only аудит с hash-chain.

**Стек:** C# 12, EF Core 10, SQLite, MathNet.Numerics (DSP), xUnit + FluentAssertions (тесты).

## Методика ведения

1. Запись **добавляется** перед каждым `git commit`. Исторические записи не правятся — только новые
   блоки в хронологическом порядке.
2. Структура записи:
   ```
   ### [Дата] Коммит: <hash или "—"> — <короткий заголовок>
   
   **План:** что собрались сделать.
   
   **Сделано:** что реально написано / изменено.
   
   **Проблемы / ловушки:** ошибки проектирования, баги, спорные решения.
   
   **Итог:** тесты, статус, что дальше.
   ```
3. Критерий завершения этапа (milestone): все тесты зелёные, коммит сделан.

---

## Текущее состояние (2026-09-09)

| Milestone | Описание | Статус |
|-----------|----------|--------|
| 1 | Skeleton solution, Directory.Build.props, .gitignore | ✅ Закрыт |
| 2 | Domain entities & enums (User, AuditEntry, ElectronicSignature) | ✅ Закрыт |
| 3 | DSP pipeline: MA → SG → ALS → PeakDetector → SignalProcessor | ✅ Закрыт (26/26 tests) |
| 4 | Persistence: CdsDbContext, EF configurations, SQLite migration, Persistence tests | ✅ Закрыт (3/3 tests) |
| 5 | Hash-chain (IHashChain), AuditTrail (IAuditTrail), AppendOnly interceptor | ✅ Закрыт (46/46 tests) |
| 6 | AuditService, SignatureService (mock), PasswordHasher | ⚪ Запланирован |
| 7 | WPF host + DI, LoginWindow, AuditView | ⚪ Запланирован |
| 8 | Sample/Method entities, AcquisitionService, LiveChart | ⚪ Запланирован |
| 9 | ReportService + CSV export | ⚪ Запланирован |

---

## Хронологический лог

### 2026-09-08 — Перевод и кодификация стандартов

**План:** перевести AGENT.md с русского, применить рекомендации Casey Muratori "Clean Code,
Horrible Performance" к hot-path коду, принять C# как целевой язык, согласовать структуру папок,
заполнить .gitignore.

**Сделано:**
- Перевод AGENT.md на английский (16 секций + code review checklist).
- Раздел 2.4 переписан для C#: `vtable → MethodTable`, `std::vector → List<T>/Span<T>`,
  добавлены 5 DOD-ограничений для hot paths (`struct` вместо `class`, `Span<T>` вместо `List<T>`,
  запрет virtual/abstract/interface на hot paths, `Unsafe.Add` для JIT-оптимизации).
- Уточнён план реализации: добавлены NSubstitute, FluentValidation, N+1 в Persistence,
  Argon2id для продакшена.
- .gitignore заполнен: .NET, IDE, SQLite `*.db`, папка `data/`, Python tooling.

**Проблемы / ловушки:**
- Структуру папок не пришлось менять — текущая `Domain / Application / Infrastructure / Wpf / Tests`
  уже соответствует Clean Architecture.
- .gitignore содержал избыточные записи по WPF — исправлено позже.

**Итог:** документы согласованы, можно начинать код.

---

### 2026-09-08 — Milestone 1: Skeleton solution

**План:** создать решение с пятью проектами, настроить зависимости, собрать без ошибок.

**Сделано:**
- `Mini-CDS.slnx` (SLNX — XML-формат для современного .NET SDK).
- Пять проектов: `MiniCds.Domain`, `MiniCds.Application`, `MiniCds.Infrastructure`,
  `MiniCds.Wpf`, `MiniCds.Tests`.
- Зависимости соблюдены: `Domain ← Application ← Infrastructure ← Wpf`, `Tests → все`.
- `Directory.Build.props`: `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors`,
  `LangVersion=latest`.

**Проблемы / ловушки:**
- WPF-проект `MiniCds.Wpf` попал в `.gitignore` из-за паттерна `*.*proj` — исправлено
  явным исключением `*.csproj` из ignore-логики.
- Сборка падала из-за отсутствия `LangVersion` на старых шаблонах — вынесено в `Directory.Build.props`.

**Итог:** `dotnet build Mini-CDS.slnx` → 0 ошибок. Milestone 1 закрыт.

---

### 2026-09-08 — Milestone 2: Domain entities

**План:** создать сущности и enum'ы в Domain-слое, отложив сложные (`Sample`, `Method`, `RawSignal`).

**Сделано:**
- Enums: `SampleStatus`, `AuditAction`, `UserRole`, `SignatureMeaning`, `InstrumentState`.
- Entities: `User`, `AuditEntry` (`sealed` для append-only), `ElectronicSignature` (`sealed`).
- `AuditEntry.Id` = `long` (не `int`) — запас на 21 CFR Part 11 журнал.
- `TimestampUtc` везде — кросс-таймзонная согласованность.

**Проблемы / ловушки:**
- `ElectronicSignature` и `AuditEntry` ссылаются друг на друга — цикл разорван отсутствием
  navigation-свойств, связи выражены скалярными id. Позже в EF это потребовало ручного
  `HasOne...HasForeignKey`, иначе FK бы не создался.

**Итог:** Domain-слой компилируется. Сложные сущности сознательно отложены до Milestone 8.

---

### 2026-09-08 — Milestone 3, часть 1: DSP components

**План:** реализовать 4 DSP-компонента по TDD: MovingAverageFilter, SavitzkyGolayFilter,
BaselineCorrector (ALS/Whittaker), PeakDetector.

**Сделано:**
- `MovingAverageFilter.Apply(ReadOnlySpan<double>, int window)` — plain `for`, DOD-style.
- `SavitzkyGolayFilter.Apply` — Vandermonde через MathNet, коэффициенты кэшируются по
  `(window, order)`.
- `BaselineCorrector.FitBaseline` — ALS-итерации, решатель = собственная O(n) pentadiagonal
  LDLᵀ-факторизация (3 плоских `double[]`, ни одного матричного объекта).
- `PeakDetector.Detect` — локальные максимумы → фильтр по высоте → prominence → метрики.
- Тесты: 21/21 зелёные до SignalProcessor.

**Проблемы / ловушки (критичные):**
1. **CS0246 SparseMatrix** — тупик в MathNet 5.0: классы `DenseMatrix`/`SparseMatrix` из
   `LinearAlgebra.Double` сделаны **internal**. Как бы ни называл `using` — тип не виден.
   Даже через публичный `Matrix<double>.Build.Sparse...` у MathNet **нет managed-решателя**
   разреженных систем (нужен нативный MKL/UMFPACK). Решение — выкинуть MathNet из ALS
   полностью и написать собственный banded LDLᵀ.
2. **Bug в FWHM** — вложенный цикл мог оставить `fwhm = null`, если левый crossing не найден,
   а правый — найден. Исправление: два независимых цикла поиска.
3. **Bug в prominence valleys** — искали минимум только между соседними кандидатами,
   а не по всему сигналу. Исправление: префиксные/суффиксные массивы минимумов O(n)
   вместо O(m·n).
4. **Неправильная граничная диагональ DᵀD** — спор с «репортёром» проверен аналитически
   и подтверждён регрессионным тестом против dense-эталона: формула `[1, 5, 6, ..., 6, 5, 1]`
   верна, а при n=3 средний = 4 (не 5). Добавлен тест `FitBaseline_BandedCoefficients_MatchDenseReference`.
5. **Опечатка в имени файла** — `BaseLineCorrector.cs` (заглавная L) вместо
   `BaselineCorrector.cs`. Класс был назван правильно, опечатка только в имени файла
   (NTFS нечувствителен, но на Linux-сборках сломается). Исправлено через `git mv`
   в два шага.

**Итог:** 21/21 DSP тест + 1 регрессионный = 22/22.

---

### 2026-09-08 — Milestone 3, часть 2: SignalProcessor orchestrator

**План:** собрать конвейер MA → SG → ALS → peak detection в один вызов, интеграционные тесты.

**Сделано:**
- `SignalProcessor : ISignalProcessor` — один публичный метод `Process`.
- Реализация: один вызов `FitBaseline` (не `Correct`, чтобы ALS не считать дважды),
  свой `for`-цикл вычитания baseline → corrected (DOD, hot path, JIT-векторизация).
- Guard: `times.Length != raw.Length → ArgumentException`.
- Интеграционные тесты: 3-пиковая хроматограмма, шум без пиков, stateless-контракт,
  mismatch-guard.

**Проблемы / ловушки:**
- В репо лежал только каркас из комментариев — CS0161 «не все пути возвращают значение».
- Порядок аргументов `ProcessedSignal(Smoothed, Baseline, Corrected, Peaks)` легко перепутать
  местами — тест это бы поймал, если бы я ошибся.

**Итог:** **26/26 зелёные**. Milestone 3 закрыт.

---

### 2026-09-09 — Milestone 4: Persistence (EF Core + SQLite)

**План:** CdsDbContext, EF-конфигурации, миграция, round-trip тесты на in-memory SQLite.

**Сделано:**
- NuGet: `Microsoft.EntityFrameworkCore.Sqlite 10.0.11`,
  `Microsoft.EntityFrameworkCore.Design 10.0.11` (у `Design` правильный `PrivateAssets`).
- `CdsDbContext` — `ApplyConfigurationsFromAssembly` (конфиги в `Persistence/Configurations/`).
- `IDesignTimeDbContextFactory<CdsDbContext>` — необходим для `dotnet ef migrations`,
  т.к. startup-project — библиотека без DI.
- `UserConfiguration`, `AuditEntryConfiguration`, `ElectronicSignatureConfiguration`:
  - таблицы в `snake_case`,
  - enums как `TEXT` (`HasConversion<string>()` — forensic-читаемость),
  - `TimestampUtc` с `SpecifyKind(Utc)` (SQLite хранит дату как TEXT и теряет Kind),
  - FK с `DeleteBehavior.Restrict` (append-only: нельзя удалить User/AuditEntry, пока
    на них ссылается юридически значимая подпись),
  - уникальный индекс `users.Username`, составной `electronic_signatures(LinkedEntityType, LinkedEntityId)`.
- Round-trip тесты `CdsDbContextTests` — SQLite `:memory:` + реальная миграция, 3 кейса:
  - enum-as-string + Utc Kind round-trip + сырая проверка через SQL `SELECT Role`,
  - уникальность username на уровне СУБД,
  - FK enforcement (`ActorUserId = 99999` → `DbUpdateException`).

**Проблемы / ловушки (критичные):**
1. **`dotnet ef` не мог создать DbContext** — `Unable to resolve DbContextOptions<CdsDbContext>`.
   Это классическая design-time ошибка: библиотека без DI не умеет сама строить options.
   Решение: `IDesignTimeDbContextFactory`.
2. **Нет навигаций → нет FK** — скалярные `AuditEntry.ActorUserId` и `ElectronicSignature.UserId`
   без navigation-свойств → EF бы создал простые `INTEGER`-колонки без констрейнтов. Исправлено
   явным `HasOne<User>().WithMany().HasForeignKey(...).OnDelete(Restrict)`.
3. **`builder.Ignore(a => a.SignatureId)` — моя приманка сработала** — строка удаляла колонку
   `SignatureId` из `audit_entries`. Миграция была сгенерирована **без неё**, пришлось
   `migrations remove → migrations add` после удаления `Ignore`.
4. **`ElectronicSignatureConfiguration.cs` не был создан** — следствие: таблица `ElectronicSignatures`
   (PascalCase вместо snake_case), `Meaning` как `INTEGER`, ноль FK. Перегенерация исправила всё.
5. **My enum name was wrong** — в тесте написал `SignatureMeaning.Approval`, а в enum — `Approved`.
   CS0117 моментально поймал. Урок: не гадать имена — читать определения.
6. **SQLite + FK** — `Microsoft.Data.Sqlite` по умолчанию включает `PRAGMA foreign_keys=ON`,
   поэтому констрейнты реально работают. Не надо было дополнительно настраивать.

**Итог:** миграция `InitialCreate` eyeball-проверена по 7 пунктам — всё зелёное. Тесты **3/3**.
Milestone 4 закрыт.

---

### 2026-09-09 — Подготовка к Milestone 5

**План (обсуждаем, код не писали):**
- `HashChain : IHashChain` в Application — чистый SHA256, length-prefix канонизация полей
  (разделитель `|` уязвим к field-boundary injection).
- `AuditTrail : IAuditTrail` в Infrastructure — вставка в `DbContext` с **детерминированным
  Id** (транзакция `BEGIN IMMEDIATE`, `max(Id)+1`), иначе `Id` неизвестен во время расчёта хэша.
- `AppendOnlyInterceptor` + SQLite-триггеры `BEFORE UPDATE/DELETE RAISE(ABORT, ...)`
  — три уровня защиты append-only.

**Открытые решения (нужно подтвердить перед Milestone 5):**
- ✅ Hash-chain включаем `Id` → **назначаем детерминированно в транзакции** (не убираем `Id` из хэша).
- ✅ Length-prefix вместо `|` в канонизации полей.
- ✅ `HashChain` в Application, `AuditTrail` в Infrastructure, `AuditService` в Application
  поверх `IAuditTrail`.

**Итог:** план согласован. Следующий шаг — `HashChain` с TDD.

---

### 2026-09-09 — Milestone 5, часть 1: HashChain (SHA256 цепочка)

**План:** реализовать `HashChain : IHashChain` в Application (чистое вычисление, без I/O)
с TDD — тесты до и вместе с реализацией.

**Сделано:**
- `src/MiniCds.Application/Audit/HashChain.cs` — `SHA256.HashData` (one-shot API, без аллокации
  хэшера), вывод — 64-символьный lowercase hex (`Convert.ToHexString(...).ToLowerInvariant()`).
- **Length-prefix канонизация** каждого поля (`длина:значение;`) вместо `|`-разделителя.
- `GenesisPrevHash = "GENESIS"` — константа-сентинел первой записи.
- `tests/MiniCds.Tests/Audit/HashChainTests.cs` — 5 методов (Theory даёт 3 кейса → 8 прогонов):
  детерминизм, формат hex, чувствительность к каждому полю, **field-boundary injection**,
  null≡empty, зависимость второй записи от хэша первой.

**Проблемы / ловушки:**
1. **`|`-разделитель уязвим**: `reason="a|b", old=""` и `reason="a", old="b"` дают одинаковый
   payload → одинаковый хэш. Злоумышленник мог бы переносить текст между полями записи
   аудита, не ломая цепочку. Length-prefix закрывает это полностью (тест
   `ComputeHash_FieldBoundaryInjection_ProducesDistinctHashes` — сторож).
2. **Uppercase/lowercase hex** — несовпадение регистра в разных местах разбило бы
   `VerifyChainAsync` на пустом месте. Зафиксирован один канон: lowercase + regex-тест.
3. **null vs empty** — SQLite хранит NULL, а хэш считается по строке. Правило:
   `null ≡ ""` до хэширования (тест `TreatsNullAsEmptyConsistently`), иначе одна и та же
   запись дала бы два разных хэша в зависимости от того, как EF материализовал колонку.

**Итог:** 8/8 зелёные. Следующий шаг — `AuditTrail : IAuditTrail` в Infrastructure
с детерминированным `Id` в транзакции (`BEGIN IMMEDIATE`, `max(Id)+1`).

---

### 2026-09-09 — Milestone 5, часть 2: AuditTrail (append-only журнал с цепочкой)

**План:** реализовать `AuditTrail : IAuditTrail` в Infrastructure (AppendAsync с детерминированным
Id, VerifyChainAsync, QueryAsync с JOIN на users) + 4 интеграционных теста на in-memory SQLite.

**Сделано:**
- `src/MiniCds.Infrastructure/Persistence/AuditTrail.cs`.
- `AuditEntryConfiguration`: `Id → ValueGeneratedNever()` — id присваивается кодом ДО вставки,
  т.к. входит в хэшируемый payload. EF при autoincrement проигнорировал бы явный Id.
- `AppendAsync`: транзакция → `max(Id)+1` → JSON-сериализация old/new ДО хэширования (в базу
  ложится тот же текст, что в payload) → `TimestampUtc.ToString("O", InvariantCulture)` —
  канонический формат, идентичный на append и verify.
- `VerifyChainAsync`: проход `ORDER BY Id`, сверка `PrevHash` + пересчёт `Hash` каждой записи.
- `QueryAsync`: явный `join` на `users` (навигаций нет по решению Milestone 2) — один SQL,
  без N+1; фильтры entityType/entityId/action/диапазон дат.
- Тесты: genesis-chain, 3 последовательных id, **tamper-тест** (сырой `UPDATE Hash` в обход EF
  → VerifyChain возвращает false), JOIN+фильтры.

**Проблемы / ловушки:**
1. **PendingModelChangesWarning** — все 4 теста упали в конструкторе: `ValueGeneratedNever`
   изменил модель, а `ModelSnapshot.cs` остался старым; EF 10 валит `Migrate()` на расхождении.
   Соблазн подавить warning через `ConfigureWarnings` отвергнут — snapshot обязан соответствовать
   модели. Лечение: `migrations remove` + повторная генерация `InitialCreate` (schema для SQLite
   не меняется — аннотация чисто design-time).
2. **Гонка Id** — SQLite однопоточен на запись, но два `AppendAsync` из разных async-контекстов
   WPF теоретически пересекаются; транзакция вокруг read-max + insert закрывает вопрос.
3. **JSON до хэша vs после** — если сериализовать в момент записи отдельно от хэширования,
   свойства могут встать в другом порядке → разные строки → verify лжёт. Одно сериализация-
   вызов, результат используется оба раза.

**Итог:** 7/7 Persistence (3 старых + 4 новых). Следующий шаг — AppendOnlyInterceptor +
SQLite-триггеры (после них tamper-тест через сырой UPDATE должен падать на уровне БД —
тест адаптируется под ожидание исключения).

---

### 2026-09-09 — Milestone 5, часть 3: Append-only защита в 3 уровня

**План:** достроить защиту неизменяемости журнала: EF-интерцептор (уровень 1) + SQLite-
триггеры (уровень 2), замкнув связку с hash-chain (уровень 3). Разделить tamper-тест на два
вектора атаки.

**Сделано:**
- `AppendOnlyInterceptor : SaveChangesInterceptor` — `SavingChanges` + `SavingChangesAsync`
  отклоняют `Modified`/`Deleted` для `AuditEntry`/`ElectronicSignature` (но не `Added`).
- Регистрация в `CdsDbContext.OnConfiguring` — действует во всех путях (тесты, design-time, DI).
- Миграция `AddAppendOnlyTriggers` — 4 триггера `BEFORE UPDATE/DELETE ... RAISE(ABORT)` на
  `audit_entries` и `electronic_signatures`; `Down` — `DROP TRIGGER IF EXISTS`.
- Tamper-тест разделён на два:
  - `RawUpdateOnAuditEntry_IsBlockedByTrigger` — уровень 2: сырой UPDATE отклоняет СУБД.
  - `VerifyChain_DetectsForgedAppend` — уровень 3: фиктивный INSERT проходит (append разрешён),
    но рвёт цепочку → `VerifyChain == false`.

**Проблемы / ловушки:**
1. **EF создал пустой каркас миграции** (`No changes were detected`) — триггеры не являются
   изменением модели. Это ожидаемо: блоки `migrationBuilder.Sql(...)` вписаны вручную.
2. **Имена таблиц в триггерах** — план предписывал `AuditTrail`, реальное имя `audit_entries`
   (из `ToTable`). Ошибка в плане, триггер на несуществующую таблицу упал бы при Migrate().
3. **Двойная регистрация интерцептора** — потенциальная ловушка на будущем DI-этапе:
   раз добавлен в `OnConfiguring`, в `AddDbContext(...AddInterceptors)` его класть НЕЛЬЗЯ.
4. **Смысловой сдвиг tamper-теста** — раньше он доказывал detection, теперь detection делится
   между двумя механизмами. Это не регресс, а уточнение модели угрозы.

**Итог:** 8/8 Persistence, полный прогон **46/46** (26 SignalProcessing + 8 HashChain +
8 Persistence + 4 TestSignalGenerator). Milestone 5 закрыт. Далее — AuditService (Application)
+ PasswordHasher (PBKDF2, Infrastructure), затем WPF-хост с DI.

---

## Архитектурные решения, зафиксированные навсегда

| Решение | Обоснование |
|---------|-------------|
| `enum` как `TEXT` в SQLite | Forensic-читаемость, дамп базы понятен человеку |
| `TimestampUtc` с `SpecifyKind(Utc)` | SQLite теряет Kind timezone — конвертация это восстанавливает |
| FK `Restrict`, не `Cascade` | Append-only семантика: нельзя удалить User/AuditEntry с активными ссылками |
| Length-prefix канонизация | `|`-разделитель уязвим к field-boundary injection |
| Детерминированный `Id` в хэше | Нужен для tamper-evidence; `max(Id)+1` в транзакции SQLite безопасен |
| Собственный O(n) banded LDLᵀ вместо MathNet | MathNet 5.0 не имеет managed sparse solve; DOD-style |
| Pre-test миграций (`Migrate()`, не `EnsureCreated()`) | Проверяет корректность самих миграций, а не только модели |
| `IDesignTimeDbContextFactory` | Канонический паттерн для библиотечных проектов без DI |
| Append-only в 3 слоя (интерцептор + триггеры + hash-chain) | Каждый слой ловит свой вектор; вместе — «изменить нельзя, подделку видно» |
| `AppendOnlyInterceptor` только в `OnConfiguring` | Гарантия защиты во всех путях; запрет дублирования в DI |

## Известные технические долги

- Файлы `BaseLineCorrector.cs` и `BaseLineCorrectorTests.cs` → переименовать через `git mv`
  в два шага (нечувствительность NTFS к регистру).
- SQLite не поддерживает `DateTimeOffset` — везде только `DateTime` + Utc Kind.
- `AuditEntry.SignatureId` — plain-колонка без FK (избегаем каскадного цикла, осознанно).
- Enum-члены нельзя переименовывать после продакшена — строковое хранение сломает чтение.
  В будущем: `[EnumMember]`-атрибуты для канонических имён.
- FluentAssertions под коммерческой лицензией — при переходе в прод заменить на Shouldly.
