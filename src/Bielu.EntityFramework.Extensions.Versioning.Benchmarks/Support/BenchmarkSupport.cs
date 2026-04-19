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

/// <summary>
/// Helpers that build SQLite-in-memory backed DbContexts wired with everything
/// the versioning subsystem needs.
/// </summary>
public static class BenchmarkContextFactory
{
    public static (VersionedBenchmarkDbContext context, SqliteConnection connection, FixedClock clock)
        CreateVersioned(DateTimeOffset clockStart)
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

        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var dbOptions = new DbContextOptionsBuilder<VersionedBenchmarkDbContext>()
            .UseSqlite(connection)
            .UseApplicationServiceProvider(appServices)
            .AddInterceptors(interceptor)
            .Options;

        var ctx = new VersionedBenchmarkDbContext(dbOptions, versioningOptions);
        ctx.Database.EnsureCreated();
        return (ctx, connection, clock);
    }

    public static (PlainBenchmarkDbContext context, SqliteConnection connection)
        CreatePlain()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var dbOptions = new DbContextOptionsBuilder<PlainBenchmarkDbContext>()
            .UseSqlite(connection)
            .Options;

        var ctx = new PlainBenchmarkDbContext(dbOptions);
        ctx.Database.EnsureCreated();
        return (ctx, connection);
    }
}
