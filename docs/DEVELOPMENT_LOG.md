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
| 6 | AuditService, SignatureService (mock), PasswordHasher | ✅ Закрыт |
| 7 | WPF host + DI (Generic Host, сидинг, LoginWindow) | ✅ Закрыт (94/94 tests) |
| 8 | Sample/Method entities, AcquisitionService, LiveChart | ✅ Закрыт (120/120 tests) |
| 9 | ReportService + CSV export + ReportDialog UI | ✅ Закрыт (140/140 tests, CSV + PDF) |

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

### 2026-09-09 — Milestone 6, часть 1: PasswordHasher (PBKDF2-SHA256)

**План:** реализовать `PasswordHasher : IPasswordHasher` в Infrastructure/Security + 6 тестов
(round-trip, неверный пароль, уникальность соли, повреждённые данные, формат, юникод).

**Сделано:**
- `Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, SHA256, 32)`; соль 16 байт —
  `RandomNumberGenerator.GetBytes` (крипто-стойкая, не `System.Random`).
- Хранение — base64; сравнение — `CryptographicOperations.FixedTimeEquals` (защита от
  timing-атак: обычное сравнение байт раскрывает позицию первого несовпадения по времени).
- `Verify` на повреждённых данных возвращает `false`, не бросает: декодирование через
  `TryFromBase64String` в буферы точного размера + сверка `bytesWritten`.

**Проблемы / ловушки:**
1. **CS7036 + CS8602** — выдуман несуществующий перегруз `TryFromBase64String(string, out byte[])`.
   Реальный пишет в переданный `Span<byte>`-буфер. Исправление даже улучшило валидацию
   (размер буфера = проверка длины бесплатно).
2. **CS1061** — неверная цепочка FluentAssertions: `NotThrow().And.BeFalse()`. Для `Func<bool>`
   значение возвращается через `.Which`: `NotThrow().Which.Should().BeFalse()`.
3. **Системный паттерн:** третий случай «API по памяти» за проект (`SignatureMeaning.Approval`,
   `TryFromBase64String`, `NotThrow().And`). Все три пойманы компилятором за секунды — но урок
   один: незнакомый API сначала смотреть в подписях (F12/доки), потом писать.

**Итог:** 6/6. Следующий шаг — `AuditService` (Application) с моком `IAuditTrail`
(NSubstitute — пакет в тестовый проект ещё НЕ добавлен, добавить на этом шаге).

---

### 2026-09-09 — Milestone 6, часть 2: AuditService (фасад Application-слоя)

**План:** `AuditService` поверх `IAuditTrail` + 7 юнит-тестов с NSubstitute-моком
(NSubstitute 6.2.0 добавлен в тестовый проект). Тестировать НЕ запись в БД (покрыто
AuditTrailTests), а **что именно** сервис запрашивает у журнала.

**Сделано:**
- `src/MiniCds.Application/Audit/AuditService.cs` — три метода: `RecordAsync` (валидация +
  форвардинг), `VerifyIntegrityAsync` (кнопка «Проверить журнал» в будущем AuditView),
  `QueryLogAsync` (сетка журнала для UI).
- Валидация: пустой/whitespace `entityType` и `actorUserId <= 0` → `ArgumentException`
  **до** обращения к хранилищу.
- `tests/MiniCds.Tests/Audit/AuditServiceTests.cs` — 7 тестов: форвардинг всех полей,
  rejection-кейсы с `DidNotReceiveWithAnyArgs()`, оба исхода `VerifyIntegrityAsync`,
  форвардинг фильтров `QueryLogAsync`.

**Проблемы / ловушки:**
1. **Async-моки требуют явной настройки возврата.** Без `.Returns(1)` NSubstitute вернул бы
   default, и `await Task<long>` дал бы 0 — тест поймал бы молчаливую подмену. Правило:
   для async-методов всегда настраивать возвращаемое значение.
2. **`ThrowAsync` для `Func<Task>`** — синхронный `Throw()` не компилируется с async-делегатами;
   FluentAssertions требует асинхронный вариант.
3. **Отложенное проектное решение — `FailedLogin`.** Кого писать в `ActorUserId` при неудачном
   входе? FK Restrict не даст записать несуществующего id. Выбран вариант «system»-аккаунт
   (сидинг), но он требует DI-хоста — вернёмся на Milestone 7. Неудачные входы пока не
   аудируются — осознанный временный пробел, зафиксирован в техдолге.
4. **`CancellationToken` в `RecordAsync` не доходит до `AppendAsync`** — в контракте
   `IAuditTrail.AppendAsync` токена нет (решение Milestone 2). Расширять контракт тайком
   не стали; явный долг.

**Итог:** 7/7 AuditServiceTests (фильтр). Полный прогон — перед коммитом. Далее —
`SignatureService` (перекрёстные id подписи↔аудита в одной транзакции) или WPF-хост.

---

### 2026-09-09 — Milestone 6, часть 3: SignatureService. Milestone закрыт.

**План:** `SignatureService : ISignatureService` (Infrastructure/Persistence) — переаутентификация
(username+password) + атомарная вставка подписи и её аудит-записи. 7 тестов на in-memory SQLite.

**Сделано:**
- Отказ (null, ничего не записано) при: неверный пароль, неизвестный/неактивный пользователь,
  **несовпадение `user.Id != actorUserId`** (Part 11: подписывает тот, кто переаутентифицировался).
- Перекрёстные ссылки `audit.SignatureId ↔ signature.AuditEntryId`: обе id известны ДО вставки
  (детерминированный max+1 в транзакции), т.к. append-only триггеры запрещают UPDATE-дописывание.
- Поправка к прошлому разговору: `SignatureId` НЕ входит в payload хэша — пересчёт хэша не нужен.
- `ElectronicSignature.Id` → `ValueGeneratedNever` (как у AuditEntry) + перегенерация InitialCreate.
- Реализация в Infrastructure (I/O + DbContext), не в Application — уточнение плана по той же
  причине, что и AuditTrail.

**Проблемы / ловушки:**
1. **CS0266 `MaxAsync`**: `IQueryable<long?>.MaxAsync()` → `Task<long?>`; `DefaultIfEmpty(0)`
   тип элемента НЕ меняет (был декоративным). Фикс — идиома `MaxAsync() ?? 0`.
2. **Ошибка команды EF (моя):** `migrations remove` снимает ПОСЛЕДНЮЮ миграцию стека. При
   стеке [InitialCreate, AddAppendOnlyTriggers] первая же remove снесла триггеры, а add InitialCreate
   упал к «name is used». Восстановлено: remove → add InitialCreate → add триггеры + SQL заново.
   **Страж сработал:** тест триггера поймал бы потерю. Правило: перед remove — `migrations list`.
3. **CS0118 в WPF (`App : Application`)**: внутри `namespace MiniCds.Wpf` простое имя `Application`
   разрешается наружу в namespace `MiniCds.Application` (слой) — член внешнего namespace ЗАТИРАЕТ
   using-алиас файла. Алиас не помог; фикс — полная квалификация `System.Windows.Application`.
   Правило WPF-слоя: `Application` всегда с полным именем. (Альтернатива — переименование namespace
   слоя — отклонена: too broad, surgical changes.)
4. Пятый «API по памяти» подряд (`MaxAsync`) — все пять пойманы компилятором до рантайма.

**Итог:** 15/15 Persistence-фильтр (8 + 7), включая зелёный тест триггеров после восстановления
миграций. Milestone 6 закрыт. Следующий — Milestone 7: WPF-хост + DI (Generic Host, сидинг
system-аккаунта → разблокирует FailedLogin-аудит).

---

### 2026-09-09 — Milestone 7, часть 1: Generic Host + DI composition root

**План:** `Host.CreateDefaultBuilder()` в App, регистрация сервисов, миграция при старте,
окно из DI, `appsettings.json` со строкой подключения. Тест валидности DI-графа.

**Сделано:**
- **Решение по lifetimes: scope на окно.** `App.OnStartup` создаёт `IServiceScope`, окно живёт
  в нём, при `Closed` — dispose. Сервисы+DbContext Scoped, хелперы (HashChain, PasswordHasher) —
  Singleton. Лечит captive dependency — классическую desktop-ловушку (WPF-объекты долгоживущие).
- `AddMiniCdsPersistence` — в **Infrastructure** (не в WPF): контракт-реализации принадлежат слою
  инфраструктуры + только так DI-граф тестируется (см. проблему 1).
- `CompositionTests` (4): `ValidateOnBuild + ValidateScopes` — контейнер проверяет граф и
  lifetimes; singleton vs scoped поведение зафиксировано тестами.
- `StartupUri` убран из App.xaml (иначе XAML создаст окно без DI); `MigrateAsync()` при старте.
- Проверено запуском: окно появляется, `data/cds.db` создан, WAL-файлы активны.

**Проблемы / ловушки:**
1. **Namespace ≠ assembly.** Wire-up лежал в `Wpf/Composition/…` с namespace `MiniCds.Infrastructure`
   — тесты (net10.0) физически не видят типы из WPF-проекта (net10.0-windows): CS1061. Лечится
   только перемещением ФАЙЛА в Infrastructure, не правкой namespace.
2. **appsettings.json не копируется в plain .NET SDK** (в отличие от Web SDK) — добавлен
   `CopyToOutputDirectory=PreserveNewest` в csproj, иначе `optional:false` валит старт.
3. **Относительный путь БД зависит от CWD процесса**, а не от папки exe: запуск из IDE создал
   `data/cds.db` в корне репозитория. `data/` в .gitignore — безопасно, но путь стоит сделать
   абсолютным (записано в техдолг).
4. `async void OnStartup` — исключение после await убьёт процесс без внятного лога. Долг:
   `DispatcherUnhandledException` + логирование.

**Итог:** сборка зелёная, CompositionTests 4/4, приложение запускается, база мигрирована.
Далее: DbSeeder (system-аккаунт + демо-пользователь) → разблокирует FailedLogin-аудит,
затем LoginWindow.

---

### 2026-09-09 — Milestone 7, часть 2: DbSeeder + Serilog structured logging

**План:** идемпотентный сидер (system-аккаунт + демо-пользователи), затем наблюдаемость
приложения: structured логи вместо «Console.WriteLine в никуда» GUI-процесса.

**Сделано:**
- `DbSeeder` (Infrastructure): `system` — без credentials (`PasswordHash=""`) и `IsActive=false`
  → интерактивный вход невозможен конструктивно; идемпотентность по username (OrdinalIgnoreCase),
  существующие пользователи не трогаются никогда.
- Демо-пароли: из `Demo__Password` (env), иначе — крипто-генерация из алфавита без неоднозначных
  символов (l/I/O/0) и **показ один раз в MessageBox** — в лог credentials не пишутся (правило).
- Сидинг не пишет аудит: автор системных событий создаётся им самим — самоссылка невозможна.
- Serilog: `UseSerilog(ReadFrom.Configuration)` + File sink с `CompactJsonFormatter`,
  суточная ротация + лимит 10 МБ, EF Core → Warning (иначе каждый SQL замусоривает файл).
- `DispatcherUnhandledException` → `Log.Fatal` + MessageBox + `Handled=true`; `CloseAndFlush` в OnExit.
- Тесты: 6 DbSeeder (включая сквозной «system не может подписать» через SignatureService и
  «оператор подписывает, цепочка валидна»). **Полный прогон: 76/76** (14 файлов).

**Проблемы / ловушки:**
1. **GUI-subsystem ≠ консольное приложение.** `dotnet run` для WinExe не блокирует терминал, а
   `Console.WriteLine` уходит в отсоединённый stdout — пароли первого сидинга были напечатаны
   «в никуда» и безвозвратны (idempotent-сидер их не пересоздаст). Лечится: логирование (сделано)
   + сброс `data/cds.db*` + `Demo__Password`. Это же объяснило «мгновенный возврат» запуска.
2. **Diff-инструмент и якорение `old_str`.** Паттерн `data/` совпал с первым вхождением — внутри
   строки комментария `# ---- data/ папка...`, а не с правилом игнора. Итог: комментарий разрезан,
   `logs/` стал частью сломанного паттерна и **не игнорировался**. Уроки: (а) `old_str` обязан быть
   уникален с окружением; (б) принятый diff перечитывать глазами — именно eyeball выявил поломку.
3. **Третий случай «арифметика по памяти ≠ факт»:** 42→46, 70→76. Правило подтверждено эмпирически:
   статус в дневник — только из вывода команды.

**Итог:** 76/76, лог пишется (`logs/cds-<date>.log`, проверено содержимое: `MiniCds host started`,
`Seeding complete: CreatedCount=3, SystemUserId=1`). Осталось в Milestone 7: AuthService + LoginWindow.

---

### 2026-09-09 — Milestone 7, часть 3: AuthService + EfUserStore + DI registration

**План:** `AuthService` (Application) с fail-closed аудитом, `IUserStore`/`EfUserStore` для read-only
доступа к пользователям, регистрация в DI, тесты резолва.

**Сделано:**
- `AuthService` (Application/Auth) — логин с переаутентификацией, dummy-верификация для unknown user
  (timing attack protection: `Lazy<(hash, salt)>` с `Guid.NewGuid()`), generic error messages
  ("Invalid username or password." для всех failure-сценариев), fail-closed при сбое аудита
  (исключение пробрасывается наверх — доступ запрещён).
- `IUserStore` (Domain/Abstractions) — минимальный контракт: `FindByUsernameAsync` + `GetSystemUserIdAsync`.
- `EfUserStore` (Infrastructure/Persistence) — `AsNoTracking` для read, fail-closed если system-аккаунт
  не засеян (`InvalidOperationException` с явным сообщением).
- Регистрация в DI: `IUserStore → EfUserStore` (Scoped), `AuthService` (Scoped).
- Тесты: 6 AuthServiceTests (NSubstitute moки, `Arg.Any<object?>()` для всех object? параметров),
  EfUserStoreTests (SQLite in-memory: сидинг → поиск, system id, fail при отсутствии system).
- CompositionTests обновлён: проверка резолва `IUserStore` и `AuthService`.
- **Полный прогон: 84/84** (76 из прошлого milestone + 6 AuthServiceTests + 2 EfUserStoreTests).

**Проблемы / ловушки:**
1. **NSubstitute AmbiguousArgumentsException** — два параметра `object?` подряд (`oldValues`, `newValues`)
   в `AppendAsync`, NSubstitute не мог различить `null`-литералы без явных matchers. Фикс: `Arg.Any<object?>()`
   для всех `object?` параметров + `Arg.Any<long?>()` для `signatureId`. Правило: если используешь matcher
   для одного аргумента типа `T`, все аргументы того же типа должны быть через matcher.
2. **DI-регистрация отсутствовала изначально** — без неё LoginWindow не смог бы резолвить `AuthService`.
   Добавлено в `ServiceCollectionExtensions` + тест резолва в `CompositionTests` (ValidateOnBuild ловит
   отсутствие регистрации на этапе сборки контейнера).
3. **Dummy-верификация для unknown user** — критично для защиты от user enumeration по времени ответа:
   `Lazy<(hash, salt)>` инициализируется один раз на процесс, `Guid.NewGuid()` обеспечивает уникальный
   секрет. Без этого attacker мог бы различать "пользователь не найден" (быстрый отказ) от "неверный пароль"
   (медленная PBKDF2-верификация).

**Итог:** 84/84, DI-граф валиден, AuthService готов к использованию в LoginWindow. Следующий шаг:
LoginWindow (WPF, окно из scope, AuthResult → смена окна на главное).

---

### 2026-09-09 — Milestone 7, часть 4: LoginWindow (WPF UI + ViewModel + DI flow)

**План:** реализовать LoginWindow (XAML + code-behind) с LoginViewModel, async-командой, DI-интеграцией.

**Сделано:**
- `LoginWindow.xaml` + `LoginWindow.xaml.cs` — модальное окно логина с полями Username/Password,
  отображением ошибки, кнопкой Login, автофокусом на UsernameBox.
- `LoginViewModel` — чистая логика с async-командой, INPC, fail-closed обработкой исключений,
  свойство `LoginResult` для передачи AuthResult наружу.
- `AsyncRelayCommand` — минимальная реализация ICommand для async-операций с `CanExecute` и защитой
  от повторного запуска.
- Конвертеры: `StringToVisibilityConverter` (скрытие ошибки), `InverseBoolConverter` (блокировка кнопки
  при загрузке).
- `IAuthService` — извлечён интерфейс из `AuthService` для тестируемости (NSubstitute не может мокать
  sealed-классы).
- DI-регистрация: `LoginWindow` как Transient, `IAuthService → AuthService` (Scoped).
- `App.xaml.cs` — логин-флоу: `ShowDialog()` → при успехе открываем `MainWindow`, при отмене — `Shutdown()`.
- Тесты: 10 `LoginViewModelTests` (начальное состояние, CanExecute-логика, success/failure/exception
  сценарии, loading-блокировка, PropertyChanged).
- **Полный прогон: 94/94** (84 из прошлого milestone + 10 LoginViewModelTests).

**Проблемы / ловушки:**
1. **NSubstitute не может мокать sealed-классы** — `AuthService` был `sealed class`, Castle DynamicProxy
  не может создать прокси. Решение: извлечь `IAuthService` интерфейс, регистрировать в DI как
  `IAuthService → AuthService`. Правило: все сервисы Application-слоя должны иметь интерфейсы для
  тестируемости.
2. **TFM несовместимость** — тестовый проект `net10.0` не может ссылаться на WPF-проект `net10.0-windows`.
  Решение: изменить TFM тестового проекта на `net10.0-windows` + `<UseWPF>true</UseWPF>`.
3. **PasswordBox не поддерживает binding** — WPF `PasswordBox.Password` не является DependencyProperty
  (безопасность). Решение: code-behind `PasswordChanged` event → ручное обновление `ViewModel.Password`.
4. **ICommand требует синхронный Execute** — `AsyncRelayCommand` реализовал только `ExecuteAsync`,
  компилятор требовал `Execute(object?)`. Решение: явная реализация `Execute` → `ExecuteAsync`.

**Итог:** 94/94, LoginWindow готов, DI-интеграция работает. Milestone 7 закрыт.

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
- `FailedLogin` не аудируется до появления system-аккаунта (сидинг, Milestone 7).
- `IAuditTrail.AppendAsync` без `CancellationToken` — расширить контракт при необходимости.
- В WPF-слое базовый класс писать `System.Windows.Application` (конфликт с namespace слоя).
- Неудачные попытки подписи (wrong password) не аудируются — до system-аккаунта (Milestone 7).
- Путь БД относительный (от CWD процесса) — сделать абсолютный от `AppContext.BaseDirectory`.
- ~~Нет `DispatcherUnhandledException`-логера~~ — закрыто (Serilog + handler + CloseAndFlush).
- Логи/БД используют относительные пути от CWD процесса (при IDE-запуске это корень репо).
  Для продакшена — абсолютные пути от `AppContext.BaseDirectory`.

---

### 2026-09-09 — Milestone 8, часть 1: Domain entities + EF configurations + SimulatorInstrumentSource

**План:** создать сущности для хроматографии (Method, Sample, RawSignal, Peak), настроить EF-конфигурации
с append-only семантикой, реализовать симулятор прибора для тестирования.

**Сделано:**
- **Domain entities** (`src/MiniCds.Domain/Entities/`):
  - `Method.cs` — версионируемый метод с DSP-параметрами (JSON-сериализация `ProcessingParameters`).
  - `Sample.cs` — образец с lifecycle (Queued → Running → Completed → Voided), void вместо delete
    (21 CFR Part 11 compliance).
  - `RawSignal.cs` — immutable сырой сигнал (byte[] для эффективности хранения).
  - `Peak.cs` — immutable пик с метриками (JSON-сериализация `PeakMetrics`), void-флаг для re-integration.
- **EF configurations** (`src/MiniCds.Infrastructure/Persistence/Configurations/`):
  - `MethodConfiguration.cs` — JSON-конвертация `Parameters`, unique index на `(Name, Version)`.
  - `SampleConfiguration.cs` — enum-to-string конвертация `Status`, FK с `DeleteBehavior.Restrict`.
  - `RawSignalConfiguration.cs` — byte[] для `Points`, FK на `Sample`.
  - `PeakConfiguration.cs` — JSON-конвертация `Metrics`, FK на `Sample` и `VoidedBy`.
- **AppendOnlyInterceptor** обновлён: `RawSignal` — fully immutable (нельзя ни UPDATE, ни DELETE),
  `Peak`/`Sample` — void вместо delete (проверка `VoidedAtUtc != null`).
- **Миграция** `AddSampleMethodRawSignalPeak` — 4 новые таблицы с FK, индексами, snake_case именами.
- **SimulatorInstrumentSource** (`src/MiniCds.Infrastructure/Instruments/`):
  - Реализация `IInstrumentSource` с event-based API (`FrameReceived`).
  - Генерация multi-Gaussian peaks в реальном времени с noise и baseline tilt.
  - `InstrumentState` transitions: Idle → Streaming → Idle.
  - `IAsyncDisposable` для graceful shutdown.
- **Тесты** (`tests/MiniCds.Tests/Instruments/SimulatorInstrumentSourceTests.cs`):
  - 6 тестов: state transitions, event raising, Gaussian peak generation, double-start protection,
    DisposeAsync cleanup.
- **IAuthService** — извлечён интерфейс из `AuthService` для тестирования (NSubstitute не мокает sealed classes).
- **LoginWindow** (`src/MiniCds.Wpf/`):
  - XAML + code-behind с Username/Password полями, error display, loading state.
  - `LoginViewModel` с async-командой, INPC, fail-closed exception handling.
  - `AsyncRelayCommand` — минимальная реализация ICommand для async-операций.
  - Конвертеры: `StringToVisibilityConverter`, `InverseBoolConverter`.
  - Интеграция в `App.xaml.cs`: LoginWindow → MainWindow flow.
- **DI registration**: `IUserStore → EfUserStore`, `AuthService`, `IAuthService → AuthService`,
  `LoginWindow` (Transient).
- **Версии пакетов** синхронизированы: EF Core 10.0.11 → 10.0.12 (устранение assembly binding conflicts).

**Проблемы / ловушки:**
1. **NSubstitute + sealed class** — `AuthService` был sealed, NSubstitute не мокает sealed classes.
   Решение: извлечь `IAuthService` интерфейс, регистрировать в DI как `IAuthService → AuthService`.
2. **NSubstitute AmbiguousArgumentsException** — тесты `AuthServiceTests` падали: NSubstitute не мог
   различить два подряд идущих параметра типа `object?` (`oldValues`, `newValues`). Решение: явные
   matchers `Arg.Any<object?>()` для всех параметров того же типа.
3. **EF Core version conflict** — Infrastructure использовал 10.0.11, WPF — 10.0.12. MSB3277 warning.
   Решение: обновить Infrastructure до 10.0.12.
4. **IInstrumentSource contract mismatch** — изначально реализовал `IAsyncEnumerable<SignalFrame>`,
   но интерфейс использует event-based паттерн (`FrameReceived`). Переписал класс и тесты.
5. **FluentAssertions API** — `HaveCountGreaterOrEqualTo` не существует, правильное имя
   `HaveCountGreaterThanOrEqualTo`. `SignalFrame` — record struct, не nullable wrapper.
6. **EF OwnsOne + readonly record struct** — `ProcessingParameters` и `PeakMetrics` — readonly record
   struct, EF Core `OwnsOne` требует класс с setter'ами. Решение: JSON-конвертация через
   `HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<T>(v, jsonOptions))`.

**Итог:** 100/100 тестов зелёные. Milestone 8 часть 1 закрыта. Следующий шаг — AcquisitionService
(оркестрация: Instrument → DSP → DB) + LiveChart (WPF, real-time visualization).

---

### 2026-09-09 — Milestone 8, часть 2: AcquisitionService + Repository pattern

**План:** реализовать оркестратор acquisition (Instrument → DSP → DB), абстрагировать persistence через
репозитории для соблюдения Clean Architecture.

**Сделано:**
- **Domain repositories** (`src/MiniCds.Domain/Abstractions/`):
  - `ISampleRepository.cs` — FindByIdAsync, UpdateStatusAsync.
  - `IMethodRepository.cs` — FindByIdAsync.
  - `IRawSignalRepository.cs` — AddAsync.
  - `IPeakRepository.cs` — AddAsync, AddRangeAsync.
- **Infrastructure implementations** (`src/MiniCds.Infrastructure/Persistence/Repositories/`):
  - `SampleRepository.cs`, `MethodRepository.cs`, `RawSignalRepository.cs`, `PeakRepository.cs`.
- **AcquisitionService** (`src/MiniCds.Application/Acquisition/AcquisitionService.cs`):
  - Подписка на `IInstrumentSource.FrameReceived`, буферизация SignalFrame.
  - Применение DSP pipeline (ISignalProcessor) к буферу кадров.
  - Сохранение RawSignal в БД (byte[] через Buffer.BlockCopy).
  - Детекция пиков и сохранение Peak entities.
  - Управление Sample lifecycle (Queued → Running → Completed).
  - Аудит всех действий через IAuditTrail.
  - События `FrameProcessed` и `PeaksDetected` для real-time UI.
- **AuditAction** enum additions: `SampleStatusChanged`, `PeakDetected`.
- **DI registration**: `ISignalProcessor → SignalProcessor` (Scoped), repositories (Scoped).
- **Тесты** (`tests/MiniCds.Tests/Acquisition/AcquisitionServiceTests.cs`):
  - 9 тестов: lifecycle transitions, RawSignal/Peak persistence, event raising, audit trail verification,
    double-start protection, invalid state handling.

**Проблемы / ловушки:**
1. **Clean Architecture violation** — Application не должен зависеть от Infrastructure. Решение:
   извлечь repository interfaces в Domain, implementations в Infrastructure.
2. **FOREIGN KEY constraint failed** — тесты падали при SeedMethodAsync: `CreatedByUserId=1` ссылался
   на несуществующего пользователя. Решение: добавить `SeedUserAsync()` перед сидированием Method/Sample.
3. **Simulator peak timing** — `RetentionTime=5.0` не попадал в 1-секундное окно теста. Решение:
   изменить на `RetentionTime=0.5, Sigma=0.1`.
4. **ISignalProcessor not registered** — CompositionTests падали: AcquisitionService требует ISignalProcessor,
   но он не был зарегистрирован в DI. Решение: добавить `services.AddScoped<ISignalProcessor, SignalProcessor>()`.
5. **IAuditTrail.AppendAsync signature** — не принимает CancellationToken. Убрал именованный параметр `ct:`
   из вызовов.
6. **IAsyncLifetime.InitializeAsync** — тестовый класс реализует IAsyncLifetime, но вся инициализация
   в конструкторе. Добавил пустой `InitializeAsync() => Task.CompletedTask`.

**Итог:** 109/109 тестов зелёные. AcquisitionService работает корректно. Следующий шаг — LiveChart
(WPF, real-time visualization) для завершения Milestone 8.

---

### 2026-09-09 — Milestone 8, часть 3: LiveChart (WPF real-time visualization)

**План:** реализовать WPF-компонент для визуализации хроматограммы в реальном времени с подпиской на
события AcquisitionService, отображением пиков и управлением acquisition.

**Сделано:**
- **LiveChartViewModel** (`src/MiniCds.Wpf/ViewModels/LiveChartViewModel.cs`):
  - `ObservableCollection<SignalFrame>` для real-time обновления графика.
  - Подписка на `AcquisitionService.FrameProcessed` и `PeaksDetected`.
  - Команды `StartAcquisitionCommand`, `StopAcquisitionCommand`, `LoadSamplesCommand`.
  - Свойства: `IsAcquiring`, `CurrentSampleName`, `PeakCount`, `SelectedSample`.
  - `Dispatcher.Invoke` для обновления UI из background thread.
- **LiveChartView** (`src/MiniCds.Wpf/Views/LiveChartView.xaml` + `.xaml.cs`):
  - WPF Canvas для рисования хроматограммы (Polyline).
  - Отображение detected peaks как вертикальные красные линии.
  - Control panel: ComboBox для выбора Sample, кнопки Start/Stop.
  - Status panel: текущий sample, статус acquiring/idle, количество пиков.
  - Автомасштабирование графика по данным.
- **BoolToStringConverter** (`src/MiniCds.Wpf/Converters/BoolToStringConverter.cs`):
  - Конвертация `bool` → "Acquiring"/"Idle" для отображения статуса.
- **IAcquisitionService** (`src/MiniCds.Domain/Abstractions/IAcquisitionService.cs`):
  - Интерфейс извлечён из `AcquisitionService` для тестируемости.
  - `AcquisitionService` реализует `IAcquisitionService`.
- **ISampleRepository.GetAllAsync** — добавлен метод для загрузки списка samples в ComboBox.
- **DI registration**: `IAcquisitionService → AcquisitionService` (Scoped).
- **Тесты** (`tests/MiniCds.Tests/Wpf/LiveChartViewModelTests.cs`):
  - 11 тестов: начальное состояние, CanExecute-логика команд, empty collections, PropertyChanged,
    sample selection.

**Проблемы / ловушки:**
1. **NSubstitute не может мокать sealed класс** — `AcquisitionService` был `sealed class`, NSubstitute
   не может создать прокси. Решение: извлечь `IAcquisitionService` интерфейс, регистрировать в DI как
   `IAcquisitionService → sp.GetRequiredService<AcquisitionService>()` (forwarding registration).
   Правило: все сервисы Application-слоя должны иметь интерфейсы для тестируемости.
2. **Application.Current namespace conflict** — в `LiveChartViewModel.cs` `Application.Current` разрешался
   в `MiniCds.Application` (слой), а не `System.Windows.Application`. Решение: полная квалификация
   `System.Windows.Application.Current.Dispatcher.Invoke`.
3. **AsyncRelayCommand constructor** — `LoadSamplesCommand` инициализирован без `canExecute` параметра,
   но конструктор требует `Func<bool>`. Решение: добавить `() => true` как всегда-разрешённый guard.
4. **LiveChartView.xaml.cs PeaksDetected property** — код ссылался на `viewModel.PeaksDetected`, но
   свойство называется `DetectedPeaks`. Решение: исправить имя свойства.
5. **ISampleRepository.GetAllAsync missing** — ViewModel вызывала `GetAllAsync()`, но метод отсутствовал
   в интерфейсе. Решение: добавить метод в `ISampleRepository` и реализовать в `SampleRepository`.

**Итог:** 120/120 тестов зелёные (109 + 11 LiveChartViewModelTests). Milestone 8 полностью закрыт.
Следующий milestone — ReportService + CSV export (Milestone 9).

---

### 2026-09-09 — Milestone 9, часть 1: Domain entities + interfaces + ReportService + CsvReportExporter

**План:** создать сущность Report, интерфейсы (IReportService, IReportExporter, IReportRepository),
реализовать CsvReportExporter и ReportService, добавить миграцию, зарегистрировать в DI.

**Сделано:**
- **Domain layer:**
  - `Report` entity — `Id, Title, Format, FilePath, SampleCount, GeneratedAtUtc, GeneratedByUserId`.
  - `IReportService` — `GenerateReportAsync(sampleIds, title, format, actorUserId, ct)`.
  - `IReportExporter` — `ExportAsync(samples, filePath, ct)`, свойство `Format`.
  - `IReportRepository` — `AddAsync`, `FindByIdAsync`.
  - `ISampleRepository.FindByIdWithPeaksAsync` — добавлен для загрузки sample с peaks в один запрос (без N+1).
  - `AuditAction.ReportGenerated` — добавлено в enum.
- **Infrastructure layer:**
  - `ReportConfiguration` — EF-конфигурация с FK на User (`GeneratedByUserId`), `DeleteBehavior.Restrict`.
  - `ReportRepository` — реализация `IReportRepository`.
  - `CsvReportExporter` — экспорт sample+peaks в CSV с `InvariantCulture`, заголовок + строки пиков.
  - `ReportService` — сбор samples через `FindByIdWithPeaksAsync`, вызов `IReportExporter`, сохранение Report в БД,
    audit-запись `ReportGenerated` через `IAuditTrail`.
  - `ServiceCollectionExtension.AddReporting()` — регистрация `IReportService`, `IReportExporter`, `IReportRepository`.
- **Миграция** `AddReportEntity` — таблица `reports` с FK, индексами, snake_case.
- **DI:** `AddReporting()` вызван в `AddMiniCdsPersistence`.

**Проблемы / ловушки:**
1. **FOREIGN KEY constraint failed** — тесты `ReportServiceTests` падали при сидировании Sample: `CreatedByUserId=1`
   ссылался на несуществующего пользователя. Решение: добавить `SeedUserAsync()` перед сидированием Method/Sample.
2. **`IReportExporter` signature mismatch** — изначально экспортер принимал `SampleReport` (не существующий тип),
   но интерфейс требовал `IReadOnlyList<Sample>`. Решение: убрать obsolete метод, привести сигнатуру к интерфейсу.
3. **`Report` record 'with' syntax** — `Report` был record, но EF требует mutable свойства. Решение: использовать
   прямое присваивание свойств вместо `with`-выражений.
4. **`AddReporting` not found** — DI-регистрация лежала в `MiniCds.Infrastructure.Reporting`, но отсутствовал
   `using` в `ServiceCollectionExtensions`. Решение: добавить `using MiniCds.Infrastructure.Reporting;`.

**Итог:** Domain/Infrastructure готовы. Следующий шаг — тесты для `CsvReportExporter` и `ReportService`.

---

### 2026-09-09 — Milestone 9, часть 2: Tests (CsvReportExporterTests + ReportServiceTests)

**План:** написать модульные и интеграционные тесты для CSV-экспортера и ReportService.

**Сделано:**
- **CsvReportExporterTests** (5 тестов):
  - single sample with peaks → валидный CSV (заголовок + N строк пиков).
  - sample without peaks → одна строка (только sample metadata).
  - multiple samples → все samples в одном файле.
  - CSV format correctness → разделители, `InvariantCulture`, порядок колонок.
  - file creation → файл создаётся по указанному пути.
- **ReportServiceTests** (5 тестов, SQLite in-memory):
  - `GenerateReportAsync` с валидными samples → создаёт Report с корректным Title/Format/FilePath.
  - вызывает `IReportExporter.ExportAsync` ровно один раз.
  - записывает audit-entry `ReportGenerated` через `IAuditTrail`.
  - persist Report в БД (проверка через `IReportRepository`).
  - несуществующий sampleId → выброс исключения.

**Проблемы / ловушки:**
1. **Дубликат `using System.IO`** — `CsvReportExporterTests.cs` имел две директивы `using System.IO`.
   Решение: удалить дубликат.
2. **FOREIGN KEY constraint failed в SeedSampleWithPeaksAsync** — как и в части 1, нужен User перед Method/Sample.
   Решение: `SeedUserAsync()` в начале теста.

**Итог:** 10 тестов Reporting проходят. Следующий шаг — UI для экспорта (ReportDialog).

---

### 2026-09-09 — Milestone 9, часть 3: ReportDialog (ViewModel + XAML + MainWindow integration)

**План:** создать WPF-диалог для выбора samples и экспорта отчёта, интегрировать в MainWindow.

**Сделано:**
- **RelayCommand** (`src/MiniCds.Wpf/Infrastructure/RelayCommand.cs`) — синхронная реализация `ICommand`
  для OK/Cancel кнопок (в отличие от `AsyncRelayCommand`, не требует async-делегата).
- **ReportDialogViewModel** (`src/MiniCds.Wpf/ViewModels/ReportDialogViewModel.cs`):
  - `InitializeAsync()` — загрузка samples из `ISampleRepository.GetAllAsync()` при открытии диалога.
  - `OkCommand` — закрывает диалог с `DialogResult=true` (только если выбран хотя бы один sample).
  - `CancelCommand` — закрывает диалог с `DialogResult=false`.
  - `GetSelectedSampleIds()` — возвращает Id выбранных samples.
  - `Action<bool> closeDialog` callback — viewModel не знает о WPF Window, только вызывает callback.
  - `SampleSelectionItem` — wrapper для Sample с `IsSelected` и `DisplayName`.
- **ReportDialog** (`src/MiniCds.Wpf/Views/ReportDialog.xaml` + `.xaml.cs`):
  - XAML: TextBox для Report Title, ListBox с CheckBox для выбора samples, индикатор загрузки,
    счётчик выбранных samples, кнопки OK/Cancel.
  - code-behind: создаёт ViewModel с `closeDialog` callback (`DialogResult = result; Close();`),
    вызывает `InitializeAsync()` в `Loaded` event.
  - Публичные свойства `GetSelectedSampleIds()` и `ReportTitle` для доступа из MainWindow.
- **MainWindow** (`src/MiniCds.Wpf/MainWindow.xaml` + `.xaml.cs`):
  - Меню `File > Export Report...` + `File > Exit`.
  - `OnExportReportClick` — открывает ReportDialog, при OK вызывает `IReportService.GenerateReportAsync`
    с `actorUserId`, показывает результат (успех/ошибка) через MessageBox.
  - Конструктор принимает `IServiceProvider`, `IReportService`, `long actorUserId`.
- **App.xaml.cs:**
  - Регистрация `ReportDialog` в DI (Transient).
  - `actorUserId` передаётся из `loginWindow.AuthResult.UserId` в `MainWindow` через
    `ActivatorUtilities.CreateInstance`.
  - `ShutdownMode="OnExplicitShutdown"` в App.xaml — иначе приложение закрывалось при закрытии LoginWindow
    до показа MainWindow.
  - `mainWindow.Closed` → `Shutdown()` для корректного завершения.
- **appsettings.json:** добавлен `Demo:Password = "demo123"` для детерминированных демо-аккаунтов.
- **Тесты** (`tests/MiniCds.Tests/Wpf/ReportDialogViewModelTests.cs`) — 10 тестов:
  - начальное состояние (ReportTitle по умолчанию, AvailableSamples пуст, IsLoading=false).
  - `InitializeAsync` загружает samples из репозитория.
  - `GetSelectedSampleIds` возвращает только выбранные.
  - `ReportTitle` изменение → PropertyChanged.
  - `SampleSelectionItem.IsSelected` → PropertyChanged.
  - `SampleSelectionItem.DisplayName` = "Name (Status)".
  - `OkCommand` с выбранными samples → закрывает диалог с `true`.
  - `CancelCommand` → закрывает диалог с `false`.

**Проблемы / ловушки:**
1. **Путь в первой строке файла** — при создании `RelayCommand.cs` и `ReportDialog.xaml` путь попадал в содержимое
   файла (`c:\Develop\...`), вызывая CS1525/MC3000. Решение: перезаписать файлы без пути в первой строке.
2. **`RelayCommand.cs` в неправильной директории** — файл оказался в `MiniCds.Infrastructure` вместо
   `MiniCds.Wpf\Infrastructure`. Решение: удалить и создать в правильном месте.
3. **`Window.Close()` не принимает параметры** — `Action<bool> closeDialog` не мог быть связан с `Close()` напрямую.
   Решение: лямбда `result => { DialogResult = result; Close(); }`.
4. **`IReportService` не найден** — в `MainWindow.xaml.cs` отсутствовал `using MiniCds.Domain.Abstractions`.
   Решение: добавить using.
5. **`actorUserId` не передавался** — `GenerateReportAsync` требует `actorUserId`, но MainWindow его не имел.
   Решение: передавать `UserId` из `AuthResult` через `ActivatorUtilities.CreateInstance`.
6. **Приложение закрывалось после логина** — `ShutdownMode` по умолчанию `OnLastWindowClose`, LoginWindow
   закрывался до показа MainWindow. Решение: `ShutdownMode="OnExplicitShutdown"` + `Shutdown()` при закрытии MainWindow.
7. **Несовпадение пароля** — после добавления `Demo:Password` существующая БД имела старые пароли (сидер не
   перезаписывает существующих пользователей). Решение: удалить `data/` для пересоздания БД.

**Итог:** 140/140 тестов зелёные (120 + 10 Reporting + 10 ReportDialogViewModel). ReportDialog работает в приложении:
логин → MainWindow → File > Export Report → выбор samples → OK → CSV-отчёт. **Осталось в Milestone 9:**
PdfReportExporter (QuestPDF). **Далее:** SignatureDialog (Milestone 10), интеграция LiveChart в MainWindow,
PeaksView, AuditView, MQTT end-to-end, README.

---

### 2026-09-09 — Milestone 9, часть 4: PdfReportExporter (QuestPDF) + выбор формата

**План:** добавить PDF-экспорт через QuestPDF, поддержать выбор формата в ReportDialog.

**Сделано:**
- **QuestPDF** — пакет `QuestPDF 2024.12.1` добавлен в `MiniCds.Infrastructure.csproj`.
  Лицензия `Community` устанавливается в статическом конструкторе `PdfReportExporter`.
- **PdfReportExporter** (`src/MiniCds.Infrastructure/Reporting/PdfReportExporter.cs`):
  - `Format = "PDF"`.
  - Документ A4: заголовок "Chromatography Report", дата генерации, для каждого sample —
    имя, метод, статус, дата создания + таблица пиков (RT, Height, Area, FWHM, Plates, Tailing).
  - Для samples без пиков — "No peaks detected.".
  - Нумерация страниц в футере.
- **ReportService** — рефакторинг:
  - Конструктор принимает `IEnumerable<IReportExporter>` вместо одного `IReportExporter`.
  - Экспортеры складываются в словарь по `Format` (case-insensitive).
  - Выбор экспортера по параметру `format`; при неизвестном формате — `NotSupportedException`.
  - Расширение файла определяется по формату (`.csv` / `.pdf`), директория `Reports/` создаётся автоматически.
- **DI** — зарегистрированы оба экспортера: `CsvReportExporter` и `PdfReportExporter`.
- **ReportDialogViewModel** — добавлены `AvailableFormats = { "CSV", "PDF" }` и `SelectedFormat` (по умолчанию "CSV").
- **ReportDialog.xaml** — ComboBox для выбора формата перед списком samples.
- **ReportDialog.xaml.cs** — публичное свойство `Format` для доступа из MainWindow.
- **MainWindow.xaml.cs** — `GenerateReportAsync` вызывается с `dialog.Format` вместо захардкоженного "CSV".

**Проблемы / ловушки:**
1. **QuestPDF `.Gray()` не существует** — `TextBlockDescriptor` не имеет метода `.Gray()`.
   Решение: использовать `.FontColor(Colors.Grey.Lighten1)`.
2. **Файл заблокирован запущенным приложением** — сборка WPF падала с MSB3027, т.к. Mini-CDS был запущен.
   Решение: закрыть приложение перед сборкой (`Stop-Process -Id 3836 -Force`).
3. **Дубликат `using System.IO`** — `CsvReportExporterTests.cs` имел две одинаковые директивы.
   Решение: удалить дубликат.

**Итог:** 140/140 тестов зелёные, 0 предупреждений. Milestone 9 полностью закрыт (CSV + PDF экспорт).
**Далее:** SignatureDialog (Milestone 10), интеграция LiveChart в MainWindow, PeaksView, AuditView,
MQTT end-to-end, README.

---

### 2026-09-09 — Интеграция LiveChart в MainWindow

**План:** встроить существующий `LiveChartView` в главное окно приложения.

**Сделано:**
- **DI:** зарегистрирован `IInstrumentSource → SimulatorInstrumentSource` в `AddMiniCdsPersistence`
  с дефолтными параметрами симулятора (4 пика, 60 сек, шум, baseline slope).
- **MainWindow.xaml:**
  - Добавлен `xmlns:views="clr-namespace:MiniCds.Wpf.Views"`.
  - Плейсхолдер `TextBlock` заменён на `<views:LiveChartView x:Name="LiveChartControl"/>`.
  - Размер окна увеличен до 1100×700.
- **MainWindow.xaml.cs:**
  - В конструкторе создаётся `LiveChartViewModel` через `ActivatorUtilities.CreateInstance(_serviceProvider, _actorUserId)`.
  - ViewModel устанавливается как `DataContext` для `LiveChartControl`.

**Итог:** 140/140 тестов зелёные. Теперь в главном окне доступен LiveChart с управлением acquизицией,
графиком сигнала и детектированными пиками. **Далее:** SignatureDialog (Milestone 10), PeaksView, AuditView,
MQTT end-to-end, README.
