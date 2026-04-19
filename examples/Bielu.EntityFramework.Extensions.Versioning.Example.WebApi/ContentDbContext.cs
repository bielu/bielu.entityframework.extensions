using Bielu.EntityFramework.Extensions.Versioning;
using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Modeling;
using Microsoft.EntityFrameworkCore;

namespace Bielu.EntityFramework.Extensions.Versioning.Example.WebApi;

/// <summary>
/// Example versioned aggregate: a piece of content keyed by a stable
/// <see cref="VersionedEntity{TEntityId, TVersionId}.EntityId"/>.
/// </summary>
public sealed class Content : VersionedEntity<Guid, Guid>
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// Example <see cref="VersionedDbContext"/> derivative — derives from the
/// base class so the <c>db.SaveAsync(...)</c> / <c>db.UpsertAsync(...)</c>
/// helpers are available directly on the context.
/// </summary>
public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options)
    : VersionedDbContext(options)
{
    public DbSet<Content> Contents => Set<Content>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyVersioning<Content, Guid, Guid>(new VersioningOptions());
    }
}
