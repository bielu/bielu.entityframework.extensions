namespace Bielu.EntityFramework.Extensions.Versioning.Saving;

/// <summary>
/// Result of a <c>Save</c> / <c>Update</c> / <c>Upsert</c> operation on
/// <see cref="VersionedDbContext"/>.
/// </summary>
/// <typeparam name="TEntity">The versioned entity type.</typeparam>
/// <param name="Entity">The persisted entity (with stamped fields).</param>
/// <param name="Kind">How the row classifies relative to the existing timeline.</param>
public sealed record VersionSaveResult<TEntity>(TEntity Entity, VersionKind Kind)
    where TEntity : class;
