using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Bielu.EntityFramework.Extensions.Versioning.Benchmarks.Support;
using Bielu.EntityFramework.Extensions.Versioning.Reading;
using Bielu.EntityFramework.Extensions.Versioning.Saving;
using Microsoft.EntityFrameworkCore;

namespace Bielu.EntityFramework.Extensions.Versioning.Benchmarks;

/// <summary>
/// Lightweight regression benchmark suite for CI/CD: focuses on core
/// versioning operations with small datasets so it completes quickly.
/// </summary>
[SimpleJob(RunStrategy.Throughput, iterationCount: 5, warmupCount: 1)]
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class RegressionBenchmark
{
    private VersionedBenchmarkHarness? _harness;
    private VersionedBenchmarkDbContext _context = null!;
    private FixedClock _clock = null!;
    private Guid[] _entityIds = null!;
    private string _bodyPayload = null!;

    /// <summary>EF Core provider under test.</summary>
    [Params(BenchmarkProvider.InMemory, BenchmarkProvider.Sqlite)]
    public BenchmarkProvider Provider { get; set; }

    /// <summary>Aggregate count for fast CI execution.</summary>
    [Params(50, 200)]
    public int AggregateCount { get; set; }

    /// <summary>Versions per aggregate to seed for read benchmarks.</summary>
    [Params(1, 10)]
    public int VersionsPerAggregate { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _harness = BenchmarkContextFactory.CreateVersioned(
            Provider, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _context = _harness.Context;
        _clock = _harness.Clock;
        _bodyPayload = new string('x', 256);

        _entityIds = new Guid[AggregateCount];
        for (var i = 0; i < AggregateCount; i++)
        {
            _entityIds[i] = Guid.NewGuid();
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => _harness?.Dispose();

    [IterationSetup(Targets = new[]
    {
        nameof(GetCurrentAsync),
        nameof(GetAllVersionsAsync),
        nameof(GetVersionCountAsync),
        nameof(UpdateAsync),
    })]
    public void SeedHistory()
    {
        // Reset table state and seed AggregateCount aggregates with VersionsPerAggregate versions each.
        _context!.Contents.RemoveRange(_context.Contents);
        _context.SaveChanges();

        var requests = new List<VersionWriteRequest<BenchmarkContent, Guid>>(_entityIds.Length * VersionsPerAggregate);
        var t0 = _clock!.UtcNow;
        for (var v = 0; v < VersionsPerAggregate; v++)
        {
            var effective = t0.AddDays(v);
            for (var i = 0; i < _entityIds.Length; i++)
            {
                requests.Add(new VersionWriteRequest<BenchmarkContent, Guid>(
                    _entityIds[i],
                    effective,
                    new BenchmarkContent { Title = $"v{v}", Body = _bodyPayload }));
            }
        }
        _context.SaveMany<BenchmarkContent, Guid, Guid>(requests);
    }

    [IterationSetup(Target = nameof(SaveAsyncBenchmark))]
    public void ResetForSave()
    {
        _context!.Contents.RemoveRange(_context.Contents);
        _context.SaveChanges();
    }

    [Benchmark(Description = "Save (initial version per aggregate)")]
    public async Task SaveAsyncBenchmark()
    {
        var t = _clock!.UtcNow;
        for (var i = 0; i < _entityIds.Length; i++)
        {
            await _context!.SaveAsync<BenchmarkContent, Guid, Guid>(
                _entityIds[i], t, new BenchmarkContent { Title = "v1", Body = _bodyPayload }).ConfigureAwait(false);
        }
    }

    [Benchmark(Description = "Update (append new version)")]
    public async Task UpdateAsync()
    {
        var t = _clock!.UtcNow.AddDays(VersionsPerAggregate + 1);
        for (var i = 0; i < _entityIds.Length; i++)
        {
            await _context!.SaveAsync<BenchmarkContent, Guid, Guid>(
                _entityIds[i], t, new BenchmarkContent { Title = "vN", Body = _bodyPayload }).ConfigureAwait(false);
        }
    }

    [Benchmark(Description = "GetCurrentAsync per aggregate")]
    public async Task GetCurrentAsync()
    {
        for (var i = 0; i < _entityIds.Length; i++)
        {
            _ = await _context!.GetCurrentAsync<BenchmarkContent, Guid, Guid>(_entityIds[i]).ConfigureAwait(false);
        }
    }

    [Benchmark(Description = "GetAllVersionsAsync per aggregate")]
    public async Task GetAllVersionsAsync()
    {
        for (var i = 0; i < _entityIds.Length; i++)
        {
            _ = await _context!.GetAllVersionsAsync<BenchmarkContent, Guid, Guid>(_entityIds[i]).ConfigureAwait(false);
        }
    }

    [Benchmark(Description = "GetVersionCountAsync per aggregate")]
    public async Task GetVersionCountAsync()
    {
        for (var i = 0; i < _entityIds.Length; i++)
        {
            _ = await _context!.GetVersionCountAsync<BenchmarkContent, Guid, Guid>(_entityIds[i]).ConfigureAwait(false);
        }
    }
}
