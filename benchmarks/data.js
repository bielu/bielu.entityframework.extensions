window.BENCHMARK_DATA = {
  "lastUpdate": 1777195321458,
  "repoUrl": "https://github.com/bielu/bielu.entityframework.extensions",
  "entries": {
    "Regression Benchmarks": [
      {
        "commit": {
          "author": {
            "name": "Arkadiusz Biel",
            "username": "bielu",
            "email": "2244074+bielu@users.noreply.github.com"
          },
          "committer": {
            "name": "GitHub",
            "username": "web-flow",
            "email": "noreply@github.com"
          },
          "id": "aac6e026f4a88b9007e71e910ddea5bc30a6c2c7",
          "message": "Merge pull request #3 from bielu/copilot/align-readme-bielu-ecosystem\n\nAlign README and project layout with bielu ecosystem; fix CI coverage step",
          "timestamp": "2026-04-19T11:49:28Z",
          "url": "https://github.com/bielu/bielu.entityframework.extensions/commit/aac6e026f4a88b9007e71e910ddea5bc30a6c2c7"
        },
        "date": 1777195321139,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.SaveAsyncBenchmark(Provider: InMemory, AggregateCount: 50, VersionsPerAggregate: 1)",
            "value": 102816704.1,
            "unit": "ns",
            "range": "± 24382082.400705263"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.UpdateAsync(Provider: InMemory, AggregateCount: 50, VersionsPerAggregate: 1)",
            "value": 59393697.75,
            "unit": "ns",
            "range": "± 6671271.878986114"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetCurrentAsync(Provider: InMemory, AggregateCount: 50, VersionsPerAggregate: 1)",
            "value": 11226407.5,
            "unit": "ns",
            "range": "± 1607763.8860781144"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetAllVersionsAsync(Provider: InMemory, AggregateCount: 50, VersionsPerAggregate: 1)",
            "value": 9750840.2,
            "unit": "ns",
            "range": "± 1200997.0845800168"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetVersionCountAsync(Provider: InMemory, AggregateCount: 50, VersionsPerAggregate: 1)",
            "value": 7811002.7,
            "unit": "ns",
            "range": "± 1113984.523439711"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.SaveAsyncBenchmark(Provider: InMemory, AggregateCount: 50, VersionsPerAggregate: 10)",
            "value": 108579156,
            "unit": "ns",
            "range": "± 39800293.532245755"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.UpdateAsync(Provider: InMemory, AggregateCount: 50, VersionsPerAggregate: 10)",
            "value": 113778419.4,
            "unit": "ns",
            "range": "± 25665484.72278307"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetCurrentAsync(Provider: InMemory, AggregateCount: 50, VersionsPerAggregate: 10)",
            "value": 12596104.25,
            "unit": "ns",
            "range": "± 4579454.485149323"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetAllVersionsAsync(Provider: InMemory, AggregateCount: 50, VersionsPerAggregate: 10)",
            "value": 11981118,
            "unit": "ns",
            "range": "± 4487298.039579534"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetVersionCountAsync(Provider: InMemory, AggregateCount: 50, VersionsPerAggregate: 10)",
            "value": 12443640.6,
            "unit": "ns",
            "range": "± 5415715.609584554"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.SaveAsyncBenchmark(Provider: InMemory, AggregateCount: 200, VersionsPerAggregate: 1)",
            "value": 312496046.6,
            "unit": "ns",
            "range": "± 72877175.90820573"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.UpdateAsync(Provider: InMemory, AggregateCount: 200, VersionsPerAggregate: 1)",
            "value": 340585804.8,
            "unit": "ns",
            "range": "± 67187305.77651526"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetCurrentAsync(Provider: InMemory, AggregateCount: 200, VersionsPerAggregate: 1)",
            "value": 59250000.4,
            "unit": "ns",
            "range": "± 1207821.8396184512"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetAllVersionsAsync(Provider: InMemory, AggregateCount: 200, VersionsPerAggregate: 1)",
            "value": 52488113.5,
            "unit": "ns",
            "range": "± 860913.9319144123"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetVersionCountAsync(Provider: InMemory, AggregateCount: 200, VersionsPerAggregate: 1)",
            "value": 45994252.25,
            "unit": "ns",
            "range": "± 520402.0856558097"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.SaveAsyncBenchmark(Provider: InMemory, AggregateCount: 200, VersionsPerAggregate: 10)",
            "value": 205081530.9,
            "unit": "ns",
            "range": "± 2233129.4671812695"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.UpdateAsync(Provider: InMemory, AggregateCount: 200, VersionsPerAggregate: 10)",
            "value": 1315076084.25,
            "unit": "ns",
            "range": "± 4238289.717469015"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetCurrentAsync(Provider: InMemory, AggregateCount: 200, VersionsPerAggregate: 10)",
            "value": 65866001.75,
            "unit": "ns",
            "range": "± 358802.54300044104"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetAllVersionsAsync(Provider: InMemory, AggregateCount: 200, VersionsPerAggregate: 10)",
            "value": 63100634.25,
            "unit": "ns",
            "range": "± 1405162.2537122122"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetVersionCountAsync(Provider: InMemory, AggregateCount: 200, VersionsPerAggregate: 10)",
            "value": 59701388.25,
            "unit": "ns",
            "range": "± 829977.8794720476"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.SaveAsyncBenchmark(Provider: Sqlite, AggregateCount: 50, VersionsPerAggregate: 1)",
            "value": 121581078.3,
            "unit": "ns",
            "range": "± 36468774.00326032"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.UpdateAsync(Provider: Sqlite, AggregateCount: 50, VersionsPerAggregate: 1)",
            "value": 307107345.75,
            "unit": "ns",
            "range": "± 38572838.596301496"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetCurrentAsync(Provider: Sqlite, AggregateCount: 50, VersionsPerAggregate: 1)",
            "value": 13064515.2,
            "unit": "ns",
            "range": "± 1195062.4277345096"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetAllVersionsAsync(Provider: Sqlite, AggregateCount: 50, VersionsPerAggregate: 1)",
            "value": 10483254.7,
            "unit": "ns",
            "range": "± 591425.1028487884"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetVersionCountAsync(Provider: Sqlite, AggregateCount: 50, VersionsPerAggregate: 1)",
            "value": 8768965.6,
            "unit": "ns",
            "range": "± 900566.8509698766"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.SaveAsyncBenchmark(Provider: Sqlite, AggregateCount: 50, VersionsPerAggregate: 10)",
            "value": 118702811.7,
            "unit": "ns",
            "range": "± 35345467.92794584"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.UpdateAsync(Provider: Sqlite, AggregateCount: 50, VersionsPerAggregate: 10)",
            "value": 106971992.2,
            "unit": "ns",
            "range": "± 17247422.244447596"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetCurrentAsync(Provider: Sqlite, AggregateCount: 50, VersionsPerAggregate: 10)",
            "value": 6370050.8,
            "unit": "ns",
            "range": "± 1646384.6265746048"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetAllVersionsAsync(Provider: Sqlite, AggregateCount: 50, VersionsPerAggregate: 10)",
            "value": 6808007.4,
            "unit": "ns",
            "range": "± 2026144.840275813"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetVersionCountAsync(Provider: Sqlite, AggregateCount: 50, VersionsPerAggregate: 10)",
            "value": 3944515.6,
            "unit": "ns",
            "range": "± 1133323.190006849"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.SaveAsyncBenchmark(Provider: Sqlite, AggregateCount: 200, VersionsPerAggregate: 1)",
            "value": 337875789,
            "unit": "ns",
            "range": "± 68291224.08891733"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.UpdateAsync(Provider: Sqlite, AggregateCount: 200, VersionsPerAggregate: 1)",
            "value": 342997761.2,
            "unit": "ns",
            "range": "± 31894605.691753116"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetCurrentAsync(Provider: Sqlite, AggregateCount: 200, VersionsPerAggregate: 1)",
            "value": 41582109.2,
            "unit": "ns",
            "range": "± 4795824.21364323"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetAllVersionsAsync(Provider: Sqlite, AggregateCount: 200, VersionsPerAggregate: 1)",
            "value": 39447331.1,
            "unit": "ns",
            "range": "± 2686538.6500050207"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetVersionCountAsync(Provider: Sqlite, AggregateCount: 200, VersionsPerAggregate: 1)",
            "value": 28150242.3,
            "unit": "ns",
            "range": "± 3804498.7566020708"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.SaveAsyncBenchmark(Provider: Sqlite, AggregateCount: 200, VersionsPerAggregate: 10)",
            "value": 205332793.6,
            "unit": "ns",
            "range": "± 25470518.587326825"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.UpdateAsync(Provider: Sqlite, AggregateCount: 200, VersionsPerAggregate: 10)",
            "value": 1222996608.5,
            "unit": "ns",
            "range": "± 4542602.688411017"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetCurrentAsync(Provider: Sqlite, AggregateCount: 200, VersionsPerAggregate: 10)",
            "value": 18101122.75,
            "unit": "ns",
            "range": "± 269763.54024141835"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetAllVersionsAsync(Provider: Sqlite, AggregateCount: 200, VersionsPerAggregate: 10)",
            "value": 17677623,
            "unit": "ns",
            "range": "± 193183.3413780805"
          },
          {
            "name": "Bielu.EntityFramework.Extensions.Versioning.Benchmarks.RegressionBenchmark.GetVersionCountAsync(Provider: Sqlite, AggregateCount: 200, VersionsPerAggregate: 10)",
            "value": 11046873.75,
            "unit": "ns",
            "range": "± 337116.92074053973"
          }
        ]
      }
    ]
  }
}