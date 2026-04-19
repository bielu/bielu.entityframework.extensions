using Bielu.EntityFramework.Extensions.Versioning.Abstractions;

namespace Bielu.EntityFramework.Extensions.Versioning.Tests.TestSupport;

/// <summary>Versioned aggregate used across test cases.</summary>
public sealed class Content : VersionedEntity<Guid, Guid>
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
