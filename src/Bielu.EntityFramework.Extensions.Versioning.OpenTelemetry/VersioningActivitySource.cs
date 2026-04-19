using System.Diagnostics;
using System.Reflection;

namespace Bielu.EntityFramework.Extensions.Versioning.OpenTelemetry;

/// <summary>
/// Provides the <see cref="ActivitySource"/> used for tracing bielu
/// content-versioning operations.
/// </summary>
public static class VersioningActivitySource
{
    private static readonly AssemblyName AssemblyName = typeof(VersioningActivitySource).Assembly.GetName();

    /// <summary>The name of the activity source.</summary>
    public static readonly string Name = AssemblyName.Name!;

    /// <summary>The version of the activity source.</summary>
    public static readonly string Version = AssemblyName.Version?.ToString() ?? "0.0.0.0";

    /// <summary>The <see cref="ActivitySource"/> for all versioning operations.</summary>
    public static readonly ActivitySource Source = new(Name, Version);

    // Operation names
    internal const string SaveVersion = "bielu.versioning.save";
    internal const string UpdateVersion = "bielu.versioning.update";
    internal const string GetCurrent = "bielu.versioning.get_current";
    internal const string GetHistory = "bielu.versioning.get_history";

    // Attribute keys
    internal const string AttributeEntityType = "versioning.entity.type";
    internal const string AttributeEntityId = "versioning.entity.id";
    internal const string AttributeEffectiveAt = "versioning.effective_at";
    internal const string AttributeVersionKind = "versioning.kind";
    internal const string AttributeVersionNumber = "versioning.number";
}
