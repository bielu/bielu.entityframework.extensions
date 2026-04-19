using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.ChangeTracking;
using Bielu.EntityFramework.Extensions.Versioning.Modeling;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bielu.EntityFramework.Extensions.Versioning.Benchmarks.Support;

/// <summary>Versioned content used by the benchmarks.</summary>
public sealed class BenchmarkContent : VersionedEntity<Guid, Guid>
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

/// <summary>Non-versioned counterpart for like-for-like benchmark comparisons.</summary>
public sealed class PlainContent
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

/// <summary>DbContext that uses the bielu versioning extensions.</summary>
public sealed class VersionedBenchmarkDbContext(DbContextOptions options, VersioningOptions versioningOptions)
    : VersionedDbContext(options)
{
    public DbSet<BenchmarkContent> Contents => Set<BenchmarkContent>();

    private VersioningOptions VersioningOptions { get; } = versioningOptions;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyVersioning<BenchmarkContent, Guid, Guid>(VersioningOptions);
    }
}

/// <summary>Plain DbContext (no versioning) used as the non-versioned baseline.</summary>
public sealed class PlainBenchmarkDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<PlainContent> Contents => Set<PlainContent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<PlainContent>(b =>
        {
            b.HasKey(x => x.Id);
        });
    }
}

/// <summary>Deterministic clock for benchmark runs.</summary>
public sealed class FixedClock(DateTimeOffset initial) : IVersioningClock
{
    private DateTimeOffset _now = initial;

    public DateTimeOffset UtcNow => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}

internal sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; } = value;
    public T Get(string? name) => CurrentValue;
    public IDisposable OnChange(Action<T, string?> listener) => NoOp.Instance;

    private sealed class NoOp : IDisposable
    {
        public static readonly NoOp Instance = new();
        public void Dispose() { }
    }
}

/// <summary>EF Core provider used by a parameterised benchmark.</summary>
public enum BenchmarkProvider
{
    InMemory = 0,
    Sqlite = 1,
}

/// <summary>
/// Disposable wrapper around a versioned benchmark context plus the (optional)
/// SQLite connection that backs it, so callers can dispose both as one unit.
/// </summary>
public sealed class VersionedBenchmarkHarness(
    VersionedBenchmarkDbContext context,
    SqliteConnection? connection,
    FixedClock clock) : IDisposable
{
    public VersionedBenchmarkDbContext Context { get; } = context;
    public FixedClock Clock { get; } = clock;

    public void Dispose()
    {
        Context.Dispose();
        connection?.Dispose();
    }
}

/// <summary>
/// Disposable wrapper around a plain (non-versioned) benchmark context plus
/// the (optional) SQLite connection that backs it.
/// </summary>
public sealed class PlainBenchmarkHarness(
    PlainBenchmarkDbContext context,
    SqliteConnection? connection) : IDisposable
{
    public PlainBenchmarkDbContext Context { get; } = context;

    public void Dispose()
    {
        Context.Dispose();
        connection?.Dispose();
    }
}

/// <summary>
/// Helpers that build DbContexts wired with everything the versioning
/// subsystem needs, on top of either the EF Core InMemory provider or
/// SQLite (in-memory) — the same two providers exercised by the unit tests.
/// </summary>
public static class BenchmarkContextFactory
{
    public static VersionedBenchmarkHarness CreateVersioned(
        BenchmarkProvider provider, DateTimeOffset clockStart)
    {
        var clock = new FixedClock(clockStart);
        var versioningOptions = new VersioningOptions();
        var monitor = new FixedOptionsMonitor<VersioningOptions>(versioningOptions);
        var interceptor = new VersioningSaveChangesInterceptor(
            clock, monitor, NullLogger<VersioningSaveChangesInterceptor>.Instance);

        var appServices = new ServiceCollection()
            .AddSingleton<IVersioningClock>(clock)
            .AddSingleton<IOptionsMonitor<VersioningOptions>>(monitor)
            .BuildServiceProvider();

        SqliteConnection? connection = null;
        DbContextOptions dbOptions;
        switch (provider)
        {
            case BenchmarkProvider.InMemory:
                dbOptions = new DbContextOptionsBuilder<VersionedBenchmarkDbContext>()
                    .UseInMemoryDatabase($"versioning-bench-{Guid.NewGuid()}")
                    .UseApplicationServiceProvider(appServices)
                    .AddInterceptors(interceptor)
                    .Options;
                break;

            case BenchmarkProvider.Sqlite:
                connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                dbOptions = new DbContextOptionsBuilder<VersionedBenchmarkDbContext>()
                    .UseSqlite(connection)
                    .UseApplicationServiceProvider(appServices)
                    .AddInterceptors(interceptor)
                    .Options;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, null);
        }

        var ctx = new VersionedBenchmarkDbContext(dbOptions, versioningOptions);
        ctx.Database.EnsureCreated();
        return new VersionedBenchmarkHarness(ctx, connection, clock);
    }

    public static PlainBenchmarkHarness CreatePlain(BenchmarkProvider provider)
    {
        SqliteConnection? connection = null;
        DbContextOptions dbOptions;
        switch (provider)
        {
            case BenchmarkProvider.InMemory:
                dbOptions = new DbContextOptionsBuilder<PlainBenchmarkDbContext>()
                    .UseInMemoryDatabase($"plain-bench-{Guid.NewGuid()}")
                    .Options;
                break;

            case BenchmarkProvider.Sqlite:
                connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                dbOptions = new DbContextOptionsBuilder<PlainBenchmarkDbContext>()
                    .UseSqlite(connection)
                    .Options;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, null);
        }

        var ctx = new PlainBenchmarkDbContext(dbOptions);
        ctx.Database.EnsureCreated();
        return new PlainBenchmarkHarness(ctx, connection);
    }
}
