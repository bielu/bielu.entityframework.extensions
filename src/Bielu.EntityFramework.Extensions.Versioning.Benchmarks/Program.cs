using BenchmarkDotNet.Running;
using Bielu.EntityFramework.Extensions.Versioning.Benchmarks;

var switcher = new BenchmarkSwitcher([
    typeof(RegressionBenchmark),
    typeof(VersionedVsNonVersionedBenchmark)
]);

switcher.Run(args, new CustomConfig());
