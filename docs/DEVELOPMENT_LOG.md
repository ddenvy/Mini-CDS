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
| 7 | WPF host + DI (Generic Host, сидинг, LoginWindow) | 🟡 В работе: хост+DI+сидинг+логи ✅, LoginWindow ⬜ |
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
