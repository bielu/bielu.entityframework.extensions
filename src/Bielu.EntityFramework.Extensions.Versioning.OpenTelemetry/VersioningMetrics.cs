using System.Diagnostics.Metrics;
using System.Reflection;

namespace Bielu.EntityFramework.Extensions.Versioning.OpenTelemetry;

/// <summary>
/// Provides the <see cref="Meter"/> and instruments used for recording bielu
/// content-versioning metrics.
/// </summary>
public static class VersioningMetrics
{
    private static readonly AssemblyName AssemblyName = typeof(VersioningMetrics).Assembly.GetName();

    /// <summary>The name of the meter.</summary>
    public static readonly string Name = AssemblyName.Name!;

    /// <summary>The version of the meter.</summary>
    public static readonly string Version = AssemblyName.Version?.ToString() ?? "0.0.0.0";

    /// <summary>The <see cref="Meter"/> for all versioning metrics.</summary>
    public static readonly Meter Meter = new(Name, Version);

    /// <summary>Total number of versions added (including initial, current and archived).</summary>
    public static readonly Counter<long> VersionsAdded = Meter.CreateCounter<long>(
        "bielu.versioning.versions.added",
        description: "Total number of versions added across all aggregates");

    /// <summary>Subset of <see cref="VersionsAdded"/> that landed strictly between two existing versions (i.e. archive inserts).</summary>
    public static readonly Counter<long> VersionsInsertedInBetween = Meter.CreateCounter<long>(
        "bielu.versioning.versions.inserted_in_between",
        description: "Total number of late-arriving versions inserted between existing ones");

    /// <summary>Number of <c>EffectiveAt</c> collisions detected by the interceptor.</summary>
    public static readonly Counter<long> VersionCollisions = Meter.CreateCounter<long>(
        "bielu.versioning.version.collisions",
        description: "Total number of EffectiveAt collisions raised by the versioning interceptor");
}
