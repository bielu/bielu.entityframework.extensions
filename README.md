# bielu.entityframework.extensions

Provider-agnostic Entity Framework Core extensions for the bielu ecosystem.

The repository currently ships:

| Package | Purpose |
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

### Install

```bash
dotnet add package Bielu.EntityFramework.Extensions.Versioning
```

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

```csharp
using Bielu.EntityFramework.Extensions.Versioning;
using Bielu.EntityFramework.Extensions.Versioning.Modeling;

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options)
    : VersionedDbContext(options)            // required base class
{
    public DbSet<Content> Contents => Set<Content>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyVersioning<Content, Guid, Guid>(new VersioningOptions());
}
```

`ApplyVersioning` sets `VersionId` as the primary key, adds a composite
unique index on `(EntityId, EffectiveAt, VersionId)` (the `VersionId`
tiebreaker means legitimate ties don't violate the index), an index on
`(EntityId, VersionNumber)` so `MAX(VersionNumber)` is an index seek, and
configures the concurrency token.

Versioning operations (`Save` / `Update` / `Upsert` / `Get*`) are exposed
exclusively as instance methods on `VersionedDbContext` — there are no
extension methods on plain `DbContext` or `DbSet<>`. This keeps the
versioning surface scoped to contexts that actually opt in to it.

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

// Cheap count: a single MAX(VersionNumber) index seek, independent of history size.
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
| `QueryFilterBehavior` | `AllVersions` | Whether to install a soft-delete query filter on versioned entities. The `VersionedDbContext` read methods always call `IgnoreQueryFilters()` themselves, so this only affects ad-hoc LINQ. Set to `AsOfNow` to hide non-current and tombstoned rows by default. |
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

## Building

```bash
dotnet build src/Bielu.EntityFramework.Extensions.slnx
dotnet test  test/Bielu.EntityFramework.Extensions.Versioning.Tests
```

The example WebApi can be run directly:

```bash
dotnet run --project examples/Bielu.EntityFramework.Extensions.Versioning.Example.WebApi
```

## License

MIT — see [`LICENSE`](LICENSE).
