using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Registration;
using Microsoft.EntityFrameworkCore;

namespace Bielu.EntityFramework.Extensions.Versioning.Reading;

/// <summary>
/// Versioning read entry points exposed as extension methods on
/// <see cref="DbSet{TEntity}"/>. Discoverable only on sets whose element type
/// satisfies the <see cref="IVersionedEntity{TEntityId, TVersionId}"/>
/// constraint, so non-versioned <see cref="DbSet{TEntity}"/>s remain
/// unaffected.
/// </summary>
/// <remarks>
/// These extensions delegate to the same internal read engine used by
/// <see cref="VersionedDbContext"/>; both surfaces are guaranteed to behave
/// identically.
/// </remarks>
public static class VersionedDbSetReadExtensions
{
    /// <summary>
    /// Returns the version current as of <paramref name="asOf"/> (or the
    /// configured <see cref="IVersioningClock"/> if <see langword="null"/>),
    /// honouring soft-delete tombstones.
    /// </summary>
    public static Task<TEntity?> GetCurrentAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningContextServices
            .Reader<TEntity, TEntityId, TVersionId>(set.ContextOf())
            .GetCurrentAsync(entityId, asOf, cancellationToken);

    /// <summary>
    /// Looks up a single version by its dedicated
    /// <see cref="IVersionedEntity{TEntityId, TVersionId}.VersionId"/>.
    /// </summary>
    public static Task<TEntity?> GetVersionAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TVersionId versionId,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningContextServices
            .Reader<TEntity, TEntityId, TVersionId>(set.ContextOf())
            .GetVersionAsync(versionId, cancellationToken);

    /// <summary>
    /// Returns the entire history of <paramref name="entityId"/>, ordered by
    /// <see cref="IVersionedEntity.EffectiveAt"/> ascending.
    /// </summary>
    public static Task<IReadOnlyList<TEntity>> GetAllVersionsAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningContextServices
            .Reader<TEntity, TEntityId, TVersionId>(set.ContextOf())
            .GetAllVersionsAsync(entityId, cancellationToken);

    /// <summary>
    /// Returns the history of <paramref name="entityId"/> within the given
    /// closed time range.
    /// </summary>
    public static Task<IReadOnlyList<TEntity>> GetHistoryAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningContextServices
            .Reader<TEntity, TEntityId, TVersionId>(set.ContextOf())
            .GetHistoryAsync(entityId, from, to, cancellationToken);

    /// <summary>
    /// Returns the predecessor and successor of <paramref name="effectiveAt"/>
    /// for <paramref name="entityId"/>.
    /// </summary>
    public static Task<VersionNeighbors<TEntity>> GetNeighborsAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningContextServices
            .Reader<TEntity, TEntityId, TVersionId>(set.ContextOf())
            .GetNeighborsAsync(entityId, effectiveAt, cancellationToken);

    /// <summary>
    /// Returns the total number of version rows recorded for
    /// <paramref name="entityId"/>. Implemented as a single
    /// <c>MAX(VersionNumber)</c> index seek.
    /// </summary>
    public static Task<int> GetVersionCountAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningContextServices
            .Reader<TEntity, TEntityId, TVersionId>(set.ContextOf())
            .GetVersionCountAsync(entityId, cancellationToken);
}
