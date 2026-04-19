# Bielu.EntityFramework.Extensions.Versioning Benchmarks

Performance benchmarking suite for `Bielu.EntityFramework.Extensions.Versioning`
using [BenchmarkDotNet](https://benchmarkdotnet.org/).

The suite mirrors the strategy used in
[bielu/Bielu.PersistentQueues](https://github.com/bielu/Bielu.PersistentQueues)
so results, dashboards, and CI workflows behave the same way across both
projects.

## Quick Start

### Run All Benchmarks

```bash
dotnet run -c Release --project src/Bielu.EntityFramework.Extensions.Versioning.Benchmarks
```

### Run a Specific Suite

```bash
# Regression benchmarks (fast, used in CI)
dotnet run -c Release --project src/Bielu.EntityFramework.Extensions.Versioning.Benchmarks -- \
  --filter "*RegressionBenchmark*"

# Versioned vs. non-versioned overhead comparison
dotnet run -c Release --project src/Bielu.EntityFramework.Extensions.Versioning.Benchmarks -- \
  --filter "*VersionedVsNonVersionedBenchmark*"

# Just the read benchmarks
dotnet run -c Release --project src/Bielu.EntityFramework.Extensions.Versioning.Benchmarks -- \
  --filter "*GetCurrent*" "*GetHistory*"
```

### Interactive Mode

```bash
dotnet run -c Release --project src/Bielu.EntityFramework.Extensions.Versioning.Benchmarks
# Then pick a benchmark from the menu.
```

## Benchmark Suites

### `RegressionBenchmark`

Lightweight, CI-friendly suite that exercises the core versioning operations
end-to-end against a SQLite in-memory database.

| Operation                   | What it measures                                             |
| --------------------------- | ------------------------------------------------------------ |
| `SaveAsync` (initial)       | Inserting the first version of each aggregate                |
| `SaveAsync` (append)        | Appending a new version onto an existing aggregate           |
| `GetCurrentAsync`           | Reading the current version per aggregate                    |
| `GetAllVersionsAsync`       | Reading the full version history per aggregate               |
| `GetVersionCountAsync`      | Counting versions per aggregate                              |

Parameters: `Provider = {InMemory, Sqlite}`, `AggregateCount = {50, 200}`, `VersionsPerAggregate = {1, 10}`.

### `VersionedVsNonVersionedBenchmark`

Side-by-side comparison so the cost of the versioning subsystem (interceptor,
extra columns, history scans, …) is visible against an equivalent EF Core
workload using a plain DbContext. Each scenario is grouped via
`[BenchmarkCategory]`, with the **plain (non-versioned)** variant set as the
`Baseline` so BenchmarkDotNet prints a `Ratio` column.

| Category      | Versioned                              | Non-versioned baseline                |
| ------------- | -------------------------------------- | ------------------------------------- |
| `Insert`      | `SaveAsync` per aggregate              | `Add` + `SaveChangesAsync` per row    |
| `BulkInsert`  | `SaveManyAsync` (single round-trip)    | `AddRange` + single `SaveChangesAsync`|
| `Update`      | `SaveAsync` (appends a new version)    | `Find` + mutate + `SaveChangesAsync`  |
| `ReadCurrent` | `GetCurrentAsync`                      | `FindAsync` (`AsNoTracking`)          |
| `ReadHistory` | `GetAllVersionsAsync`                  | `Where(id).ToListAsync()`             |

> Both contexts run side-by-side on the same EF Core provider, parameterised
> over **InMemory** and **SQLite (in-memory)** — the same providers exercised
> by the unit tests. The measured delta therefore reflects the overhead of
> the versioning logic itself rather than the storage engine.

## CI/CD Integration

Three workflows live under `.github/workflows/`:

| Workflow                  | Trigger                              | Purpose                                                                                            |
| ------------------------- | ------------------------------------ | -------------------------------------------------------------------------------------------------- |
| `benchmark-pr.yml`        | Pull requests touching the project   | Runs `RegressionBenchmark` and posts results as a PR comment, failing the run if a regression > 15% is detected. |
| `benchmark-baseline.yml`  | Push to `main` (or manual dispatch)  | Refreshes the historical baseline stored on the `gh-pages` branch.                                  |
| `benchmark-weekly.yml`    | Weekly cron (Sundays 02:00 UTC)      | Long-running run on a fresh runner; updates the dashboard with a more relaxed alert threshold.      |

The PR workflow uses [`benchmark-action/github-action-benchmark`](https://github.com/benchmark-action/github-action-benchmark)
and compares against the JSON pushed by the baseline workflow.

## Analysing Results

| Metric    | Description                                                | Target            |
| --------- | ---------------------------------------------------------- | ----------------- |
| Mean      | Arithmetic mean of all measurements                        | Lower is better   |
| Error     | Half of the 99.9 % confidence interval                     | Lower is better   |
| StdDev    | Standard deviation                                         | Lower is better   |
| Allocated | Managed-memory allocation per operation                    | Lower is better   |
| Ratio     | Versioned / non-versioned (only on the comparison suite)   | Track over time   |

### Interpreting Changes

- **< 5 %** — within noise margin
- **5–10 %** — potentially significant, investigate
- **> 10 %** — significant, document the reason in the PR

## Best Practices

- Always run with `-c Release`.
- Close other applications and disable CPU frequency scaling for stable
  results.
- Compare results from the **same machine**; cross-machine numbers are not
  comparable.
- Check both timing and `Allocated`; an allocation regression often precedes
  a latency one.

## Adding New Benchmarks

1. Add a new method on `RegressionBenchmark` only if it should run on every PR
   (keep the whole regression run under a few minutes).
2. Use `[Params]` for varying input shapes.
3. When measuring versioning overhead, add a paired non-versioned baseline to
   `VersionedVsNonVersionedBenchmark` and mark it `Baseline = true`.
4. Document the benchmark with an XML comment.

## Resources

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [`benchmark-action/github-action-benchmark`](https://github.com/benchmark-action/github-action-benchmark)
- [Bielu.PersistentQueues benchmarks](https://github.com/bielu/Bielu.PersistentQueues/tree/main/src/Bielu.PersistentQueues.Benchmarks)
