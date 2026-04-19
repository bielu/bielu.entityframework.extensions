# Bielu.EntityFramework.Extensions

[![CI](https://github.com/bielu/bielu.entityframework.extensions/actions/workflows/buildAndPublishPackage.yml/badge.svg)](https://github.com/bielu/bielu.entityframework.extensions/actions/workflows/buildAndPublishPackage.yml)
[![NuGet](https://img.shields.io/nuget/v/Bielu.EntityFramework.Extensions.Versioning.svg)](https://www.nuget.org/packages/Bielu.EntityFramework.Extensions.Versioning/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Bielu.EntityFramework.Extensions.Versioning.svg)](https://www.nuget.org/packages/Bielu.EntityFramework.Extensions.Versioning/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Bielu.EntityFramework.Extensions is a set of **provider-agnostic Entity Framework Core extensions** that add cross-cutting persistence concerns — starting with content versioning — to any EF Core model without relying on temporal tables, triggers, or provider-specific SQL.

Part of the [**bielu ecosystem**](https://github.com/bielu) — a collection of open-source .NET libraries for search, messaging, observability, and infrastructure.

> ⚠️ **Note:** Pre version 1.0.0, the API is regarded as unstable and **breaking changes may be introduced**.

## Key Features

- ✅ **Content versioning for any aggregate** — stable `EntityId` + per-version `VersionId`, with support for inserting versions between two existing ones (late / out-of-order updates)
- ✅ **Provider-agnostic** — works on every EF Core relational and non-relational provider (SqlServer, Postgres, MySQL, SQLite, Cosmos, InMemory, …); no `SYSTEM_VERSIONING`, no temporal tables, no triggers, no provider-specific JSON, no `xmin`, no raw SQL
- ✅ **Two equivalent surfaces** — instance methods on `VersionedDbContext` and extension methods on `DbSet<T>` constrained to versioned entities, so you can opt one entity in without changing your context base class
- ✅ **Save-changes interceptor** — auto-assigns `VersionId`, stamps `RecordedAt`, surfaces collisions as a clear `EffectiveAtCollisionException`, and enforces immutability of past versions
- ✅ **Soft delete with tombstones** — preserves history; configurable global query filter
- ✅ **Bulk writes** — `SaveMany` / `UpdateMany` / `UpsertMany` share a single `SaveChangesAsync` (one transaction on relational providers)
- ✅ **OpenTelemetry instrumentation** — optional companion package adds `ActivitySource` spans and metrics (`versions.added`, `versions.inserted_in_between`, `version.collisions`)
- ✅ **Benchmarked in CI** — [BenchmarkDotNet](https://benchmarkdotnet.org/) suites run on every PR with a [live dashboard](https://bielu.github.io/bielu.entityframework.extensions/dev/bench/)

## Installation

Install the packages from NuGet:

```bash
# Core EF Core implementation
dotnet add package Bielu.EntityFramework.Extensions.Versioning

# Abstractions only (no EF Core dependency) — reference from domain layers
dotnet add package Bielu.EntityFramework.Extensions.Versioning.Abstractions

# (Optional) OpenTelemetry instrumentation
dotnet add package Bielu.EntityFramework.Extensions.Versioning.OpenTelemetry
```

## Packages

| Package | Description |
| --- | --- |
| `Bielu.EntityFramework.Extensions.Versioning.Abstractions` | Contracts (no EF Core dependency) so domain layers can reference them. |
| `Bielu.EntityFramework.Extensions.Versioning` | EF Core implementation: model configuration, `VersionedDbContext`, save-changes interceptor, DI helpers. |
| `Bielu.EntityFramework.Extensions.Versioning.OpenTelemetry` | Optional `ActivitySource` + metrics for observability. |

---

## Content versioning

Adds **content versioning** to any aggregate, where:

- An aggregate has a **stable "general" id** (`EntityId`) shared by every version.
- Each version row has its **own primary key** (`VersionId`).
- New versions can be **inserted between two existing versions** to handle out-of-order / late updates.
- Every operation works on **every EF Core relational and non-relational provider** — no `SYSTEM_VERSIONING`, no temporal tables, no triggers, no provider-specific JSON, no `xmin`, no raw SQL.

### Define a versioned aggregate

```csharp
using Bielu.EntityFramework.Extensions.Versioning.Abstractions;

public sealed class Content : VersionedEntity<Guid, Guid>
{
    public string Title { get; set; } = string.Empty;
    public string Body  { get; set; } = string.Empty;
}
```

`VersionedEntity<TEntityId, TVersionId>` provides `EntityId`, `VersionId`,
`EffectiveAt`, `RecordedAt`, `IsDeleted`, `VersionNumber` and a concurrency
token out of the box.

### Configure the model

The recommended pattern is to derive your context from `VersionedDbContext`,
which makes the versioning operations available as instance methods on the
context itself:

```csharp
using Bielu.EntityFramework.Extensions.Versioning;
using Bielu.EntityFramework.Extensions.Versioning.Modeling;

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options)
    : VersionedDbContext(options)            // optional but recommended
{
    public DbSet<Content> Contents => Set<Content>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyVersioning<Content, Guid, Guid>(new VersioningOptions());
}
```

Inheritance is **not** required — the `DbSet<T>` extension surface
(described below) works on any `DbContext` derivative as long as
`ApplyVersioning<…>()` has been called for the entity. Use
`VersionedDbContext` when you want a single one-stop registration via
`AddVersionedDbContext<TContext>(...)`; use a plain `DbContext` when you
want to opt one entity into versioning without changing your context base
class.

`ApplyVersioning` sets `VersionId` as the primary key, adds a composite
unique index on `(EntityId, EffectiveAt, VersionId)` (the `VersionId`
tiebreaker means legitimate ties don't violate the index), an index on
`(EntityId, VersionNumber)` so the timeline can be ordered by insertion,
and configures the concurrency token.

Versioning operations (`Save` / `Update` / `Upsert` / `Get*`) are exposed
on two surfaces — instance methods on `VersionedDbContext` and extension
methods on `DbSet<TEntity>` constrained to
`IVersionedEntity<TEntityId, TVersionId>`. There are deliberately **no**
extension methods on plain `DbContext`: that keeps the versioning surface
scoped to the entities that actually opt in to it.

### Register DI

`AddVersionedDbContext<TContext>` is a one-liner that internally calls
`AddBieluVersioning()`, wires `UseApplicationServiceProvider`, and
registers the `VersioningSaveChangesInterceptor` for you. Pass your
provider configuration as the callback:

```csharp
using Bielu.EntityFramework.Extensions.Versioning.Registration;

builder.Services.AddVersionedDbContext<ContentDbContext>((_, options) =>
    options.UseSqlite("DataSource=content.db"));
```

If you need full control, the building blocks are still public:

```csharp
builder.Services.AddBieluVersioning();                        // clock + interceptor + options
builder.Services.AddDbContext<ContentDbContext>((sp, options) =>
{
    options.UseSqlite("DataSource=content.db");
    options.UseApplicationServiceProvider(sp);
    options.AddInterceptors(sp.GetRequiredService<VersioningSaveChangesInterceptor>());
});
```

### Write versions

Two equivalent surfaces are available — pick whichever fits your call-site:

| Surface | Example |
| --- | --- |
| `VersionedDbContext` instance method | `await db.SaveAsync<Content, Guid, Guid>(id, effectiveAt, payload);` |
| `DbSet<T>` extension | `await db.Contents.SaveAsync<Content, Guid, Guid>(id, effectiveAt, payload);` |

Both surfaces delegate to the same internal save engine, so they are
guaranteed to behave identically — and you can mix them freely in the same
context. The `DbSet<T>` extensions only light up on sets whose element
implements `IVersionedEntity<TEntityId, TVersionId>`, so they don't pollute
IntelliSense on non-versioned sets. The `DbSet<T>` extensions also work on
plain `DbContext` derivatives (you don't have to inherit from
`VersionedDbContext` to use them) — handy when you want to opt one entity
type into versioning without changing your context base class.

`Save` / `Update` / `Upsert` (and their `Many` and async variants) are
provided. Every write returns a `VersionSaveResult<TEntity>` whose `Kind`
classifies the row as one of:

- `Initial` — first version of the aggregate.
- `Current` — appended to the head of the timeline.
- `Archive` — back-dated; sits in the past portion of the timeline.

```csharp
var first  = await db.SaveAsync<Content, Guid, Guid>(id, T1, new Content { Title = "v1" });   // Initial
var second = await db.SaveAsync<Content, Guid, Guid>(id, T3, new Content { Title = "v3" });   // Current

// Late-arriving update at T2 (T1 < T2 < T3): no renumbering, no special API —
// just call SaveAsync with the in-between EffectiveAt.
var inBetween = await db.SaveAsync<Content, Guid, Guid>(id, T2, new Content { Title = "v2" });
inBetween.Kind.ShouldBe(VersionKind.Archive);
```

#### Bulk writes

`SaveMany`, `UpdateMany` and `UpsertMany` accept a sequence of
`VersionWriteRequest<TEntity, TEntityId>` and persist them in a single
underlying `SaveChangesAsync` call so they share one transaction on
relational providers:

```csharp
await db.SaveManyAsync<Content, Guid, Guid>(new[]
{
    new VersionWriteRequest<Content, Guid>(id, T1, new Content { Title = "v1" }),
    new VersionWriteRequest<Content, Guid>(id, T2, new Content { Title = "v2" }),
    new VersionWriteRequest<Content, Guid>(id, T3, new Content { Title = "v3" }),
});
```

### Read versions

The same dual surface is available for reads — `VersionedDbContext`
instance methods or `DbSet<T>` extensions, your pick:

```csharp
// Current (default: as of now). Honours soft-delete tombstones.
var current = await db.Contents.GetCurrentAsync<Content, Guid, Guid>(id);
// or:        await db.GetCurrentAsync<Content, Guid, Guid>(id);

// Time-travel.
var snapshot = await db.Contents.GetCurrentAsync<Content, Guid, Guid>(id, asOf: DateTimeOffset.UtcNow.AddYears(-1));

// Full ordered timeline.
IReadOnlyList<Content> all = await db.Contents.GetAllVersionsAsync<Content, Guid, Guid>(id);

// Total number of versions for the aggregate. Implemented as a single
// COUNT against the (EntityId, ...) index — provably correct under
// concurrent writes (MAX(VersionNumber) was previously used here as a
// micro-optimisation, but VersionNumber is best-effort under concurrency
// and not safe to count from).
int total = await db.Contents.GetVersionCountAsync<Content, Guid, Guid>(id);

// Predecessor / successor of an EffectiveAt point — useful for diffing late inserts.
var (prev, next) = await db.Contents.GetNeighborsAsync<Content, Guid, Guid>(id, T2)
    switch { var n => (n.Previous, n.Next) };
```

### Soft delete

`SoftDeleteVersionAsync` writes a tombstone row at the given `EffectiveAt`.
The aggregate's history is preserved; only `GetCurrentAsync` returns `null`
when the tombstone is the latest version.

### Options reference

| Option | Default | Behaviour |
| --- | --- | --- |
| `VersionIdStrategy` | `NewGuid` | How `VersionId`s are auto-assigned by the interceptor when not pre-populated. `CallerProvided` disables auto-assignment. |
| `QueryFilterBehavior` | `AllVersions` | Whether to install a soft-delete query filter on versioned entities. The versioned read methods always call `IgnoreQueryFilters()` themselves, so this only affects ad-hoc LINQ. Set to `HideTombstones` to hide soft-deleted rows by default. |
| `InPlaceUpdateBehavior` | `Throw` | What to do when an EF-tracked versioned entity becomes `Modified`. `Throw` enforces immutability; `ConvertToNewVersion` automatically promotes the change to a new version row; `Allow` bypasses the guard for administrative scenarios. |
| `EffectiveAtPrecision` | `Tick` (verbatim) | Granularity to which incoming `EffectiveAt` timestamps are rounded before persisting (`Tick`, `Microsecond`, `Millisecond`, `Second`). |
| `DetectCollisionsExplicitly` | `true` | When `true`, the interceptor surfaces `EffectiveAtCollisionException` with a clear message before the provider raises an opaque unique-constraint error. |

### Observability

Add the optional companion package and the `ActivitySource` /
`Meter` are wired up automatically:

```bash
dotnet add package Bielu.EntityFramework.Extensions.Versioning.OpenTelemetry
```

Emitted metrics: `versions.added`, `versions.inserted_in_between`,
`version.collisions`.

### Supported providers

The implementation relies only on standard EF Core building blocks
(`IEntityTypeConfiguration`, `HasIndex`, query filters, value converters,
`SaveChangesInterceptor`, LINQ `OrderBy` / `Max`). No raw SQL, no
provider-specific options, no temporal tables. Tested against:

- `Microsoft.EntityFrameworkCore.InMemory`
- `Microsoft.EntityFrameworkCore.Sqlite`

Other relational providers (SqlServer, Postgres, MySQL, …) and Cosmos work
without any code changes; just plug in their EF Core provider package.

### What this package deliberately does not do

- It does **not** use `SYSTEM_VERSIONING` / temporal tables / triggers /
  `xmin`. Those are provider-specific and break portability.
- It does **not** audit non-versioned entities. That's a separate concern;
  use a dedicated audit package alongside this one if you need it.
- It does **not** automatically promote arbitrary EF mutations into new
  versions unless `InPlaceUpdateBehavior.ConvertToNewVersion` is opted into.
  The default is to surface mutations as a clear `InvalidOperationException`
  so accidental in-place edits are loud.

---

## Benchmarks

A [BenchmarkDotNet](https://benchmarkdotnet.org/) suite lives under
[`src/Bielu.EntityFramework.Extensions.Versioning.Benchmarks`](src/Bielu.EntityFramework.Extensions.Versioning.Benchmarks/README.md)
and runs in CI (`benchmark-pr.yml` on every PR, `benchmark-baseline.yml` on
`main`, `benchmark-weekly.yml` on a weekly cron). It contains two suites:

- **`RegressionBenchmark`** — fast CI guard for `SaveAsync`, append, `GetCurrentAsync`, `GetAllVersionsAsync`, `GetVersionCountAsync`.
- **`VersionedVsNonVersionedBenchmark`** — pairs each versioned operation with the closest plain EF Core equivalent against a parallel non-versioned schema; the plain variant is the BenchmarkDotNet `Baseline`, so the `Ratio` column shows the cost of versioning directly.

Both suites are parameterised over the same two providers exercised by the
unit tests (EF Core **InMemory** and **SQLite** in-memory) so you can see how
the overhead changes between the two.

📈 **Live dashboard:** <https://bielu.github.io/bielu.entityframework.extensions/dev/bench/>
(populated by the baseline and weekly workflows; the dashboard is empty until
those have run on `main` for the first time.)

### Current results

Captured on a 2-core AMD EPYC 9V74 GitHub-hosted runner, .NET 10.0.5,
`BenchmarkDotNet v0.15.8`. Numbers are means; lower is better. See the
benchmark project's [README](src/Bielu.EntityFramework.Extensions.Versioning.Benchmarks/README.md)
for the full methodology.

#### Regression suite (versioning operations)

`AggregateCount = 200`, `VersionsPerAggregate = 10`, payload ≈ 256 B.

| Operation                                 | Provider | Mean       | Allocated   |
| ----------------------------------------- | -------- | ---------: | ----------: |
| `SaveAsync` (initial version per aggregate) | InMemory | 161.3 ms   | 85.5 MB     |
| `SaveAsync` (initial version per aggregate) | SQLite   | 143.8 ms   | 74.6 MB     |
| `GetCurrentAsync` per aggregate            | InMemory | 48.2 ms    | 96.8 MB     |
| `GetCurrentAsync` per aggregate            | SQLite   |  9.1 ms    |  3.2 MB     |

`AggregateCount = 50`, `VersionsPerAggregate = 1`:

| Operation                                 | Provider | Mean       | Allocated   |
| ----------------------------------------- | -------- | ---------: | ----------: |
| `SaveAsync` (initial)                      | InMemory |  54.0 ms   |  6.99 MB    |
| `SaveAsync` (initial)                      | SQLite   | 116.5 ms   |  6.98 MB    |
| `GetCurrentAsync`                          | InMemory |   5.7 ms   |  1.34 MB    |
| `GetCurrentAsync`                          | SQLite   |   8.8 ms   |  0.82 MB    |

#### Versioned vs. non-versioned (cost of the versioning subsystem)

Each row reports the **versioned mean** and the **ratio** vs. the plain
non-versioned baseline (1.00 = same speed; 2.00 = versioned is twice as slow).
`Alloc Ratio` is allocated bytes vs. the same baseline.

`AggregateCount = 200`, `VersionsPerAggregate = 10`:

| Scenario                       | Provider | Versioned mean | Time ratio | Alloc ratio |
| ------------------------------ | -------- | -------------: | ---------: | ----------: |
| `Insert` (one SaveChanges/row) | InMemory | 164.5 ms       | 3.69×      | 14.6×       |
| `Insert` (one SaveChanges/row) | SQLite   | 187.9 ms       | 1.61×      | 10.2×       |
| `ReadCurrent`                  | InMemory |  47.4 ms       | 12.6×      | 23.1×       |
| `ReadCurrent`                  | SQLite   |   9.4 ms       | 2.07×      | 1.5×        |

`AggregateCount = 50`, `VersionsPerAggregate = 1`:

| Scenario                       | Provider | Versioned mean | Time ratio | Alloc ratio |
| ------------------------------ | -------- | -------------: | ---------: | ----------: |
| `Insert`                       | InMemory |  98.6 ms       | 31.3×      | 15.8×       |
| `Insert`                       | SQLite   | 104.6 ms       | 13.4×      |  8.6×       |
| `ReadCurrent`                  | InMemory |   4.8 ms       |  1.58×     |  2.3×       |
| `ReadCurrent`                  | SQLite   |   9.6 ms       |  1.39×     |  1.5×       |

Notes:

- The InMemory provider is unrealistically fast for the **plain** baseline
  (no parsing, no SQL pipeline), which inflates the ratios — InMemory ratios
  are useful for catching regressions in the *versioning* code path itself,
  while SQLite ratios are closer to what real workloads look like.
- The Insert ratios shrink as `AggregateCount` grows because the versioning
  bookkeeping is amortised over more rows in a single `SaveChanges`; using
  `SaveManyAsync` (covered by the `BulkInsert` category in the comparison
  suite) shrinks them further.
- `ReadCurrent` overhead in SQLite is dominated by the extra ordering /
  `EffectiveAt` filter that the versioning query layers on top of a primary-key
  lookup; the cost per call is sub-millisecond.

---

## Building

```bash
dotnet build src/Bielu.EntityFramework.Extensions.slnx
dotnet test  src/Bielu.EntityFramework.Extensions.Versioning.Tests
```

The example WebApi can be run directly:

```bash
dotnet run --project examples/Bielu.EntityFramework.Extensions.Versioning.Example.WebApi
```

## License

MIT — see [`LICENSE`](LICENSE).
