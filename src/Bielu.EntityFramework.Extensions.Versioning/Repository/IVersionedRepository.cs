using Bielu.EntityFramework.Extensions.Versioning.Abstractions;

namespace Bielu.EntityFramework.Extensions.Versioning.Repository;

/// <summary>
/// Classifies a version row at the moment it is persisted.
/// </summary>
public enum VersionKind
{
    /// <summary>
    /// The very first version recorded for this <c>EntityId</c>.
    /// </summary>
    Initial = 0,

    /// <summary>
    /// The newly persisted row is the latest version of the aggregate — its
    /// <see cref="IVersionedEntity.EffectiveAt"/> is greater than or equal to
    /// every previously recorded version's <see cref="IVersionedEntity.EffectiveAt"/>
    /// for the same <c>EntityId</c>.
    /// </summary>
    Current = 1,

    /// <summary>
    /// A back-dated / late-arriving version. Its
    /// <see cref="IVersionedEntity.EffectiveAt"/> is strictly less than the
    /// most recent existing version's, so it sits in the past portion of the
    /// timeline (possibly between two existing versions).
    /// </summary>
    Archive = 2,
}

/// <summary>
/// Result of a <c>Save</c> / <c>Update</c> operation.
/// </summary>
/// <typeparam name="TEntity">The versioned entity type.</typeparam>
/// <param name="Entity">The persisted entity (with stamped fields).</param>
/// <param name="Kind">How the row classifies relative to the existing timeline.</param>
public sealed record VersionSaveResult<TEntity>(TEntity Entity, VersionKind Kind)
    where TEntity : class;

/// <summary>
/// A predecessor/successor pair of versions surrounding a given
/// <c>EffectiveAt</c> point on a versioned aggregate's timeline.
/// </summary>
/// <typeparam name="TEntity">The versioned entity type.</typeparam>
public sealed record VersionNeighbors<TEntity>
    where TEntity : class
{
    /// <summary>
    /// The version that immediately precedes the queried <c>EffectiveAt</c>
    /// (i.e. the version with the greatest <c>EffectiveAt</c> &lt;= the query
    /// point). <see langword="null"/> when no such version exists.
    /// </summary>
    public TEntity? Previous { get; init; }

    /// <summary>
    /// The version that immediately follows the queried <c>EffectiveAt</c>
    /// (i.e. the version with the smallest <c>EffectiveAt</c> &gt; the query
    /// point). <see langword="null"/> when no such version exists.
    /// </summary>
    public TEntity? Next { get; init; }
}

/// <summary>
/// Provider-agnostic repository abstraction for content-versioned entities.
/// </summary>
/// <typeparam name="TEntity">The versioned entity type.</typeparam>
/// <typeparam name="TEntityId">Aggregate identifier type.</typeparam>
/// <typeparam name="TVersionId">Per-version identifier type.</typeparam>
public interface IVersionedRepository<TEntity, TEntityId, TVersionId>
    where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
    where TEntityId : notnull
    where TVersionId : notnull
{
    /// <summary>
    /// Persists <paramref name="payload"/> as a new version row for
    /// <paramref name="entityId"/> at the given <paramref name="effectiveAt"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SaveAsync"/> is the canonical "upsert" entry point: it
    /// works whether or not the aggregate already has any versions. The
    /// returned <see cref="VersionSaveResult{TEntity}"/> classifies the row
    /// as <see cref="VersionKind.Initial"/>, <see cref="VersionKind.Current"/>
    /// or <see cref="VersionKind.Archive"/> based on
    /// <paramref name="effectiveAt"/> versus the existing timeline — so the
    /// caller does not have to inspect history to know which case occurred.
    /// </para>
    /// <para>
    /// Inserting a version <i>between</i> two existing versions (a late /
    /// out-of-order update) is just a <see cref="SaveAsync"/> call with an
    /// <paramref name="effectiveAt"/> that falls between their timestamps; no
    /// renumbering of existing rows is required.
    /// </para>
    /// </remarks>
    /// <param name="entityId">The aggregate identifier.</param>
    /// <param name="effectiveAt">The business-time at which the version becomes effective.</param>
    /// <param name="payload">The payload to persist as a new version.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<VersionSaveResult<TEntity>> SaveAsync(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous counterpart to <see cref="SaveAsync"/>.
    /// </summary>
    VersionSaveResult<TEntity> Save(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload);

    /// <summary>
    /// Persists <paramref name="payload"/> as a new version of an
    /// <i>existing</i> aggregate (one that already has at least one version
    /// recorded for <paramref name="entityId"/>). Throws
    /// <see cref="InvalidOperationException"/> when the aggregate does not
    /// yet exist; use <see cref="SaveAsync"/> for upsert semantics.
    /// </summary>
    /// <remarks>
    /// Like <see cref="SaveAsync"/>, the returned
    /// <see cref="VersionSaveResult{TEntity}"/> reports whether the new row is
    /// the new current version or an archived (back-dated) one.
    /// </remarks>
    Task<VersionSaveResult<TEntity>> UpdateAsync(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous counterpart to <see cref="UpdateAsync"/>.
    /// </summary>
    VersionSaveResult<TEntity> Update(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload);

    /// <summary>
    /// Returns the version that is current as of <paramref name="asOf"/>
    /// (i.e. the version with the greatest
    /// <see cref="IVersionedEntity.EffectiveAt"/> &lt;= <paramref name="asOf"/>).
    /// When <paramref name="asOf"/> is <see langword="null"/>, the
    /// <see cref="IVersioningClock"/> is consulted for the current instant.
    /// </summary>
    /// <returns>
    /// The current (non-deleted) version, or <see langword="null"/> if no
    /// version exists at the queried point.
    /// </returns>
    Task<TEntity?> GetCurrentAsync(
        TEntityId entityId,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a version by its dedicated <see cref="IVersionedEntity{TEntityId,TVersionId}.VersionId"/>.
    /// </summary>
    Task<TEntity?> GetVersionAsync(
        TVersionId versionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the entire history of <paramref name="entityId"/>, ordered by
    /// <see cref="IVersionedEntity.EffectiveAt"/> ascending.
    /// </summary>
    Task<IReadOnlyList<TEntity>> GetAllVersionsAsync(
        TEntityId entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the history of <paramref name="entityId"/> within the given
    /// closed time range, ordered by <see cref="IVersionedEntity.EffectiveAt"/>
    /// ascending.
    /// </summary>
    Task<IReadOnlyList<TEntity>> GetHistoryAsync(
        TEntityId entityId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the predecessor and successor of <paramref name="effectiveAt"/>
    /// for <paramref name="entityId"/>. Useful for diffing a late-arriving
    /// update against the surrounding versions.
    /// </summary>
    Task<VersionNeighbors<TEntity>> GetNeighborsAsync(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total number of version rows recorded for
    /// <paramref name="entityId"/>. The implementation issues a single index
    /// seek (<c>MAX(VersionNumber)</c>) — independent of the number of
    /// versions — so the operation remains cheap even for aggregates with
    /// thousands of versions.
    /// </summary>
    Task<int> GetVersionCountAsync(
        TEntityId entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a tombstone version (i.e. a version with
    /// <see cref="IVersionedEntity.IsDeleted"/> set to <see langword="true"/>)
    /// at the given <paramref name="effectiveAt"/>. The aggregate's history
    /// is preserved; only the as-of-now read returns <see langword="null"/>.
    /// </summary>
    Task<VersionSaveResult<TEntity>> SoftDeleteVersionAsync(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity tombstonePayload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard-removes a single version row from the database. Use sparingly —
    /// removing a version breaks the immutable-history invariant.
    /// </summary>
    Task RemoveVersionAsync(
        TVersionId versionId,
        CancellationToken cancellationToken = default);
}
