using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bielu.EntityFramework.Extensions.Versioning.Tests.TestSupport;

internal sealed class TestDbContext(DbContextOptions options, VersioningOptions versioningOptions)
    : DbContext(options)
{
    public DbSet<Content> Contents => Set<Content>();

    private VersioningOptions VersioningOptions { get; } = versioningOptions;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyVersioning<Content, Guid, Guid>(VersioningOptions);
    }
}

/// <summary>Indicates the EF Core provider used by a parameterised test.</summary>
public enum TestProvider
{
    InMemory = 0,
    Sqlite = 1,
}

internal sealed class TestHarness : IAsyncDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection? _sqliteConnection;

    public TestDbContext Context { get; }
    public FakeVersioningClock Clock { get; }
    public VersioningOptions Options { get; }
    public IOptionsMonitor<VersioningOptions> OptionsMonitor { get; }
    public VersioningSaveChangesInterceptor Interceptor { get; }

    private TestHarness(
        TestDbContext context,
        FakeVersioningClock clock,
        VersioningOptions options,
        IOptionsMonitor<VersioningOptions> monitor,
        VersioningSaveChangesInterceptor interceptor,
        Microsoft.Data.Sqlite.SqliteConnection? sqliteConnection)
    {
        Context = context;
        Clock = clock;
        Options = options;
        OptionsMonitor = monitor;
        Interceptor = interceptor;
        _sqliteConnection = sqliteConnection;
    }

    public static async Task<TestHarness> CreateAsync(
        TestProvider provider,
        VersioningOptions? options = null,
        DateTimeOffset? clockStart = null)
    {
        var versioningOptions = options ?? new VersioningOptions();
        var clock = new FakeVersioningClock(clockStart ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var monitor = new TestOptionsMonitor<VersioningOptions>(versioningOptions);
        var interceptor = new VersioningSaveChangesInterceptor(
            clock, monitor, NullLogger<VersioningSaveChangesInterceptor>.Instance);

        Microsoft.Data.Sqlite.SqliteConnection? sqliteConnection = null;
        DbContextOptions dbOptions;

        switch (provider)
        {
            case TestProvider.InMemory:
                dbOptions = new DbContextOptionsBuilder<TestDbContext>()
                    .UseInMemoryDatabase($"versioning-{Guid.NewGuid()}")
                    .AddInterceptors(interceptor)
                    .Options;
                break;

            case TestProvider.Sqlite:
                sqliteConnection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
                await sqliteConnection.OpenAsync().ConfigureAwait(false);
                dbOptions = new DbContextOptionsBuilder<TestDbContext>()
                    .UseSqlite(sqliteConnection)
                    .AddInterceptors(interceptor)
                    .Options;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, null);
        }

        var ctx = new TestDbContext(dbOptions, versioningOptions);
        await ctx.Database.EnsureCreatedAsync().ConfigureAwait(false);
        return new TestHarness(ctx, clock, versioningOptions, monitor, interceptor, sqliteConnection);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync().ConfigureAwait(false);
        if (_sqliteConnection is not null)
        {
            await _sqliteConnection.DisposeAsync().ConfigureAwait(false);
        }
    }
}

internal sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; private set; } = value;
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
    public void Set(T value) => CurrentValue = value;
}
