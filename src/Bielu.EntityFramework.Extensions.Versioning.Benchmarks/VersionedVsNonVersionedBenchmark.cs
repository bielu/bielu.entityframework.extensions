using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Bielu.EntityFramework.Extensions.Versioning.Benchmarks.Support;
using Bielu.EntityFramework.Extensions.Versioning.Reading;
using Bielu.EntityFramework.Extensions.Versioning.Saving;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bielu.EntityFramework.Extensions.Versioning.Benchmarks;

/// <summary>
/// Side-by-side comparison of versioned and non-versioned EF Core operations
/// so the overhead of the versioning subsystem can be tracked over time.
///
/// Each benchmark has a <c>Versioned</c> and a <c>Plain</c> variant performing
/// the closest equivalent work against parallel schemas backed by the same
/// SQLite-in-memory provider.
/// </summary>
[SimpleJob(RunStrategy.Throughput, iterationCount: 5, warmupCount: 1)]
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class VersionedVsNonVersionedBenchmark
{
    private VersionedBenchmarkDbContext? _versionedContext;
    private SqliteConnection? _versionedConnection;
    private FixedClock? _clock;

    private PlainBenchmarkDbContext? _plainContext;
    private SqliteConnection? _plainConnection;

    private Guid[] _ids = null!;
    private string _body = null!;

    [Params(50, 200)]
    public int AggregateCount { get; set; }

    /// <summary>Number of historical versions/rows pre-seeded for read benchmarks.</summary>
    [Params(1, 10)]
    public int VersionsPerAggregate { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var (vctx, vconn, clock) = BenchmarkContextFactory.CreateVersioned(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _versionedContext = vctx;
        _versionedConnection = vconn;
        _clock = clock;

        var (pctx, pconn) = BenchmarkContextFactory.CreatePlain();
        _plainContext = pctx;
        _plainConnection = pconn;

        _body = new string('x', 256);
        _ids = new Guid[AggregateCount];
        for (var i = 0; i < AggregateCount; i++)
        {
            _ids[i] = Guid.NewGuid();
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _versionedContext?.Dispose();
        _versionedConnection?.Dispose();
        _plainContext?.Dispose();
        _plainConnection?.Dispose();
    }

    // -----------------------------------------------------------------------
    // Insert
    // -----------------------------------------------------------------------

    [IterationSetup(Targets = new[] { nameof(Insert_Versioned), nameof(Insert_Plain) })]
    public void ResetForInsert()
    {
        _versionedContext!.Contents.RemoveRange(_versionedContext.Contents);
        _versionedContext.SaveChanges();
        _plainContext!.Contents.RemoveRange(_plainContext.Contents);
        _plainContext.SaveChanges();
    }

    [BenchmarkCategory("Insert"), Benchmark(Description = "Versioned: SaveAsync (initial)")]
    public async Task Insert_Versioned()
    {
        var t = _clock!.UtcNow;
        for (var i = 0; i < _ids.Length; i++)
        {
            await _versionedContext!.SaveAsync<BenchmarkContent, Guid, Guid>(
                _ids[i], t, new BenchmarkContent { Title = "v1", Body = _body }).ConfigureAwait(false);
        }
    }

    [BenchmarkCategory("Insert"), Benchmark(Baseline = true, Description = "Plain: Add + SaveChanges")]
    public async Task Insert_Plain()
    {
        for (var i = 0; i < _ids.Length; i++)
        {
            _plainContext!.Contents.Add(new PlainContent
            {
                Id = _ids[i],
                Title = "v1",
                Body = _body,
            });
            await _plainContext.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    // -----------------------------------------------------------------------
    // Bulk insert (single SaveChanges call)
    // -----------------------------------------------------------------------

    [IterationSetup(Targets = new[] { nameof(BulkInsert_Versioned), nameof(BulkInsert_Plain) })]
    public void ResetForBulkInsert() => ResetForInsert();

    [BenchmarkCategory("BulkInsert"), Benchmark(Description = "Versioned: SaveManyAsync")]
    public async Task BulkInsert_Versioned()
    {
        var t = _clock!.UtcNow;
        var requests = new List<VersionWriteRequest<BenchmarkContent, Guid>>(_ids.Length);
        for (var i = 0; i < _ids.Length; i++)
        {
            requests.Add(new VersionWriteRequest<BenchmarkContent, Guid>(
                _ids[i], t, new BenchmarkContent { Title = "v1", Body = _body }));
        }
        await _versionedContext!.SaveManyAsync<BenchmarkContent, Guid, Guid>(requests).ConfigureAwait(false);
    }

    [BenchmarkCategory("BulkInsert"), Benchmark(Baseline = true, Description = "Plain: AddRange + SaveChanges")]
    public async Task BulkInsert_Plain()
    {
        var rows = new List<PlainContent>(_ids.Length);
        for (var i = 0; i < _ids.Length; i++)
        {
            rows.Add(new PlainContent { Id = _ids[i], Title = "v1", Body = _body });
        }
        _plainContext!.Contents.AddRange(rows);
        await _plainContext.SaveChangesAsync().ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Update / append-new-version
    // -----------------------------------------------------------------------

    [IterationSetup(Targets = new[] { nameof(Update_Versioned), nameof(Update_Plain), nameof(GetCurrent_Versioned), nameof(GetCurrent_Plain), nameof(GetHistory_Versioned), nameof(GetHistory_Plain) })]
    public void SeedHistory()
    {
        _versionedContext!.Contents.RemoveRange(_versionedContext.Contents);
        _versionedContext.SaveChanges();
        _plainContext!.Contents.RemoveRange(_plainContext.Contents);
        _plainContext.SaveChanges();

        // Versioned: VersionsPerAggregate distinct version rows per aggregate.
        var t0 = _clock!.UtcNow;
        var requests = new List<VersionWriteRequest<BenchmarkContent, Guid>>(_ids.Length * VersionsPerAggregate);
        for (var v = 0; v < VersionsPerAggregate; v++)
        {
            var effective = t0.AddDays(v);
            for (var i = 0; i < _ids.Length; i++)
            {
                requests.Add(new VersionWriteRequest<BenchmarkContent, Guid>(
                    _ids[i], effective, new BenchmarkContent { Title = $"v{v}", Body = _body }));
            }
        }
        _versionedContext.SaveMany<BenchmarkContent, Guid, Guid>(requests);

        // Plain: only the latest "current" row exists per aggregate (no history kept).
        var rows = new List<PlainContent>(_ids.Length);
        for (var i = 0; i < _ids.Length; i++)
        {
            rows.Add(new PlainContent { Id = _ids[i], Title = $"v{VersionsPerAggregate - 1}", Body = _body });
        }
        _plainContext.Contents.AddRange(rows);
        _plainContext.SaveChanges();
    }

    [BenchmarkCategory("Update"), Benchmark(Description = "Versioned: SaveAsync (append)")]
    public async Task Update_Versioned()
    {
        var t = _clock!.UtcNow.AddDays(VersionsPerAggregate + 1);
        for (var i = 0; i < _ids.Length; i++)
        {
            await _versionedContext!.SaveAsync<BenchmarkContent, Guid, Guid>(
                _ids[i], t, new BenchmarkContent { Title = "vN", Body = _body }).ConfigureAwait(false);
        }
    }

    [BenchmarkCategory("Update"), Benchmark(Baseline = true, Description = "Plain: Find + Update + SaveChanges")]
    public async Task Update_Plain()
    {
        for (var i = 0; i < _ids.Length; i++)
        {
            var entity = await _plainContext!.Contents.FindAsync(_ids[i]).ConfigureAwait(false);
            if (entity is not null)
            {
                entity.Title = "vN";
                entity.Body = _body;
                await _plainContext.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Read: current / latest
    // -----------------------------------------------------------------------

    [BenchmarkCategory("ReadCurrent"), Benchmark(Description = "Versioned: GetCurrentAsync")]
    public async Task GetCurrent_Versioned()
    {
        for (var i = 0; i < _ids.Length; i++)
        {
            _ = await _versionedContext!.GetCurrentAsync<BenchmarkContent, Guid, Guid>(_ids[i]).ConfigureAwait(false);
        }
    }

    [BenchmarkCategory("ReadCurrent"), Benchmark(Baseline = true, Description = "Plain: FindAsync")]
    public async Task GetCurrent_Plain()
    {
        for (var i = 0; i < _ids.Length; i++)
        {
            _ = await _plainContext!.Contents.AsNoTracking().FirstOrDefaultAsync(c => c.Id == _ids[i]).ConfigureAwait(false);
        }
    }

    // -----------------------------------------------------------------------
    // Read: history / single row
    // -----------------------------------------------------------------------

    [BenchmarkCategory("ReadHistory"), Benchmark(Description = "Versioned: GetAllVersionsAsync")]
    public async Task GetHistory_Versioned()
    {
        for (var i = 0; i < _ids.Length; i++)
        {
            _ = await _versionedContext!.GetAllVersionsAsync<BenchmarkContent, Guid, Guid>(_ids[i]).ConfigureAwait(false);
        }
    }

    [BenchmarkCategory("ReadHistory"), Benchmark(Baseline = true, Description = "Plain: ToList by id")]
    public async Task GetHistory_Plain()
    {
        // Non-versioned baseline: only one row per aggregate exists, so this is
        // a single-row read. It illustrates the cost of EF Core's query
        // pipeline without the versioning ordering/filtering layered on top.
        for (var i = 0; i < _ids.Length; i++)
        {
            _ = await _plainContext!.Contents.AsNoTracking().Where(c => c.Id == _ids[i]).ToListAsync().ConfigureAwait(false);
        }
    }
}
