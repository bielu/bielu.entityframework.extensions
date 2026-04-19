using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Reading;
using Bielu.EntityFramework.Extensions.Versioning.Registration;
using Bielu.EntityFramework.Extensions.Versioning.Saving;
using Microsoft.EntityFrameworkCore;

namespace Bielu.EntityFramework.Extensions.Versioning;

/// <summary>
/// <see cref="DbContext"/> base class that exposes the bielu content
/// versioning <c>Save</c> / <c>Update</c> / <c>Upsert</c> entry points (and
/// their <c>Many</c> / async siblings) plus all the read helpers
/// (<c>GetCurrentAsync</c>, <c>GetAllVersionsAsync</c>,
/// <c>GetVersionCountAsync</c>, <c>GetHistoryAsync</c>,
/// <c>GetNeighborsAsync</c>, <c>GetVersionAsync</c>) directly on the context.
/// </summary>
/// <remarks>
/// <para>
/// Versioning operations are exposed via two equivalent surfaces:
/// <see cref="VersionedDbContext"/> instance methods (defined here) and
/// <see cref="DbSet{TEntity}"/> extension methods (in the
/// <c>Bielu.EntityFramework.Extensions.Versioning.Saving</c> /
/// <c>.Reading</c> namespaces). Both delegate to the same internal save and
/// read engines — pick whichever reads better at the call-site. The
/// <see cref="DbSet{TEntity}"/> extensions also work on plain
/// <see cref="DbContext"/> derivatives (no requirement to inherit this base
/// class), so versioning can be opted into per-entity rather than per-context
/// when desired.
/// </para>
/// <para>
/// When you do derive from <see cref="VersionedDbContext"/>, register the
/// context with <c>services.AddVersionedDbContext&lt;TContext&gt;(...)</c>,
/// which wires the application service provider and the
/// <c>VersioningSaveChangesInterceptor</c> for you.
/// </para>
/// <para>
/// The <see cref="IVersioningClock"/> and
/// <see cref="IOptionsMonitor{TOptions}"/> are resolved from the EF Core
/// service provider (which honours <c>UseApplicationServiceProvider</c>);
/// in the unconfigured / test scenario the context falls back to a system
/// clock and the default <see cref="VersioningOptions"/>.
/// </para>
/// </remarks>
public abstract class VersionedDbContext : DbContext
{
    /// <inheritdoc cref="DbContext()" />
    protected VersionedDbContext()
    {
    }

    /// <inheritdoc cref="DbContext(DbContextOptions)" />
    protected VersionedDbContext(DbContextOptions options) : base(options)
    {
    }

    // -----------------------------------------------------------------------
    // Save / SaveAsync (upsert: works on new or existing aggregates)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Persists <paramref name="payload"/> as a new version row for
    /// <paramref name="entityId"/> at the given <paramref name="effectiveAt"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="SaveAsync{TEntity, TEntityId, TVersionId}(TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>
    /// is the canonical "upsert" entry point: it works whether or not the
    /// aggregate already has any versions. The returned
    /// <see cref="VersionSaveResult{TEntity}"/> classifies the row as
    /// <see cref="VersionKind.Initial"/>, <see cref="VersionKind.Current"/> or
    /// <see cref="VersionKind.Archive"/> based on
    /// <paramref name="effectiveAt"/> versus the existing timeline — so the
    /// caller does not have to inspect history to know which case occurred.
    /// </remarks>
    public Task<VersionSaveResult<TEntity>> SaveAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(payload);
        return Saver<TEntity, TEntityId, TVersionId>()
            .SaveSingleAsync(entityId, effectiveAt, payload, requireExisting: false, cancellationToken);
    }

    /// <summary>Synchronous counterpart to
    /// <see cref="SaveAsync{TEntity, TEntityId, TVersionId}(TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public VersionSaveResult<TEntity> Save<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => SaveAsync<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, payload).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    /// <summary>
    /// Bulk variant of
    /// <see cref="SaveAsync{TEntity, TEntityId, TVersionId}(TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.
    /// All requests are persisted in a single underlying
    /// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> call so they
    /// share one transaction on relational providers.
    /// </summary>
    public Task<IReadOnlyList<VersionSaveResult<TEntity>>> SaveManyAsync<TEntity, TEntityId, TVersionId>(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(requests);
        return Saver<TEntity, TEntityId, TVersionId>()
            .SaveManyCoreAsync(requests, requireExisting: false, cancellationToken);
    }

    /// <summary>Synchronous counterpart to
    /// <see cref="SaveManyAsync{TEntity, TEntityId, TVersionId}(IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)"/>.</summary>
    public IReadOnlyList<VersionSaveResult<TEntity>> SaveMany<TEntity, TEntityId, TVersionId>(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => SaveManyAsync<TEntity, TEntityId, TVersionId>(requests).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    // -----------------------------------------------------------------------
    // Update / UpdateAsync (requires the aggregate to already exist)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Persists <paramref name="payload"/> as a new version of an
    /// <i>existing</i> aggregate (one that already has at least one version
    /// recorded for <paramref name="entityId"/>). Throws
    /// <see cref="InvalidOperationException"/> when the aggregate does not yet
    /// exist; use
    /// <see cref="SaveAsync{TEntity, TEntityId, TVersionId}(TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>
    /// or
    /// <see cref="UpsertAsync{TEntity, TEntityId, TVersionId}(TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>
    /// for create-or-update semantics.
    /// </summary>
    public Task<VersionSaveResult<TEntity>> UpdateAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(payload);
        return Saver<TEntity, TEntityId, TVersionId>()
            .SaveSingleAsync(entityId, effectiveAt, payload, requireExisting: true, cancellationToken);
    }

    /// <summary>Synchronous counterpart to
    /// <see cref="UpdateAsync{TEntity, TEntityId, TVersionId}(TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public VersionSaveResult<TEntity> Update<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => UpdateAsync<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, payload).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    /// <summary>Bulk variant of
    /// <see cref="UpdateAsync{TEntity, TEntityId, TVersionId}(TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public Task<IReadOnlyList<VersionSaveResult<TEntity>>> UpdateManyAsync<TEntity, TEntityId, TVersionId>(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(requests);
        return Saver<TEntity, TEntityId, TVersionId>()
            .SaveManyCoreAsync(requests, requireExisting: true, cancellationToken);
    }

    /// <summary>Synchronous counterpart to
    /// <see cref="UpdateManyAsync{TEntity, TEntityId, TVersionId}(IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)"/>.</summary>
    public IReadOnlyList<VersionSaveResult<TEntity>> UpdateMany<TEntity, TEntityId, TVersionId>(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => UpdateManyAsync<TEntity, TEntityId, TVersionId>(requests).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    // -----------------------------------------------------------------------
    // Upsert / UpsertAsync (single & many)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Create-or-update entry point. Equivalent to
    /// <see cref="SaveAsync{TEntity, TEntityId, TVersionId}(TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>
    /// — provided as a separate method so domain code can read at the
    /// call-site exactly what was intended. The returned
    /// <see cref="VersionSaveResult{TEntity}"/> reports whether the operation
    /// effectively created the aggregate (<see cref="VersionKind.Initial"/>)
    /// or appended to an existing one (<see cref="VersionKind.Current"/> /
    /// <see cref="VersionKind.Archive"/>).
    /// </summary>
    public Task<VersionSaveResult<TEntity>> UpsertAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => SaveAsync<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, payload, cancellationToken);

    /// <summary>Synchronous counterpart to
    /// <see cref="UpsertAsync{TEntity, TEntityId, TVersionId}(TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public VersionSaveResult<TEntity> Upsert<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => Save<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, payload);

    /// <summary>Bulk variant of
    /// <see cref="UpsertAsync{TEntity, TEntityId, TVersionId}(TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public Task<IReadOnlyList<VersionSaveResult<TEntity>>> UpsertManyAsync<TEntity, TEntityId, TVersionId>(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => SaveManyAsync<TEntity, TEntityId, TVersionId>(requests, cancellationToken);

    /// <summary>Synchronous counterpart to
    /// <see cref="UpsertManyAsync{TEntity, TEntityId, TVersionId}(IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)"/>.</summary>
    public IReadOnlyList<VersionSaveResult<TEntity>> UpsertMany<TEntity, TEntityId, TVersionId>(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => SaveMany<TEntity, TEntityId, TVersionId>(requests);

    // -----------------------------------------------------------------------
    // Soft delete & hard remove
    // -----------------------------------------------------------------------

    /// <summary>
    /// Adds a tombstone version (i.e. a version with
    /// <see cref="IVersionedEntity.IsDeleted"/> set to <see langword="true"/>)
    /// at the given <paramref name="effectiveAt"/>. The aggregate's history
    /// is preserved; only the as-of-now read returns <see langword="null"/>.
    /// </summary>
    public Task<VersionSaveResult<TEntity>> SoftDeleteVersionAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity tombstonePayload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(tombstonePayload);
        tombstonePayload.IsDeleted = true;
        return SaveAsync<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, tombstonePayload, cancellationToken);
    }

    /// <summary>
    /// Hard-removes a single version row from the database. Use sparingly —
    /// removing a version breaks the immutable-history invariant.
    /// </summary>
    public async Task RemoveVersionAsync<TEntity, TEntityId, TVersionId>(
        TVersionId versionId,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        var existing = await Set<TEntity>()
            .IgnoreQueryFilters()
            .Where(e => e.VersionId.Equals(versionId))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return;
        }

        Set<TEntity>().Remove(existing);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Read helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the version that is current as of <paramref name="asOf"/>
    /// (i.e. the version with the greatest
    /// <see cref="IVersionedEntity.EffectiveAt"/> &lt;= <paramref name="asOf"/>).
    /// When <paramref name="asOf"/> is <see langword="null"/>, the
    /// <see cref="IVersioningClock"/> is consulted for the current instant.
    /// Honours soft-delete tombstones — returns <see langword="null"/> when
    /// the latest version &lt;= <paramref name="asOf"/> is a tombstone.
    /// </summary>
    public Task<TEntity?> GetCurrentAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => Reader<TEntity, TEntityId, TVersionId>().GetCurrentAsync(entityId, asOf, cancellationToken);

    /// <summary>
    /// Looks up a version by its dedicated
    /// <see cref="IVersionedEntity{TEntityId,TVersionId}.VersionId"/>.
    /// </summary>
    public Task<TEntity?> GetVersionAsync<TEntity, TEntityId, TVersionId>(
        TVersionId versionId,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => Reader<TEntity, TEntityId, TVersionId>().GetVersionAsync(versionId, cancellationToken);

    /// <summary>
    /// Returns the entire history of <paramref name="entityId"/>, ordered by
    /// <see cref="IVersionedEntity.EffectiveAt"/> ascending.
    /// </summary>
    public Task<IReadOnlyList<TEntity>> GetAllVersionsAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => Reader<TEntity, TEntityId, TVersionId>().GetAllVersionsAsync(entityId, cancellationToken);

    /// <summary>
    /// Returns the history of <paramref name="entityId"/> within the given
    /// closed time range, ordered by <see cref="IVersionedEntity.EffectiveAt"/>
    /// ascending.
    /// </summary>
    public Task<IReadOnlyList<TEntity>> GetHistoryAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => Reader<TEntity, TEntityId, TVersionId>().GetHistoryAsync(entityId, from, to, cancellationToken);

    /// <summary>
    /// Returns the predecessor and successor of <paramref name="effectiveAt"/>
    /// for <paramref name="entityId"/>. Useful for diffing a late-arriving
    /// update against the surrounding versions.
    /// </summary>
    public Task<VersionNeighbors<TEntity>> GetNeighborsAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => Reader<TEntity, TEntityId, TVersionId>().GetNeighborsAsync(entityId, effectiveAt, cancellationToken);

    /// <summary>
    /// Returns the total number of version rows recorded for
    /// <paramref name="entityId"/>. The implementation issues a single index
    /// seek (<c>MAX(VersionNumber)</c>) — independent of the number of
    /// versions — so the operation remains cheap even for aggregates with
    /// thousands of versions.
    /// </summary>
    public Task<int> GetVersionCountAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => Reader<TEntity, TEntityId, TVersionId>().GetVersionCountAsync(entityId, cancellationToken);

    // -----------------------------------------------------------------------
    // Internals: build the per-call saver/reader, resolving services from
    // the EF Core service provider (which honours UseApplicationServiceProvider)
    // with sensible fallbacks for unconfigured / test scenarios.
    //
    // Both this base class and the DbSet<TEntity> extension methods route
    // through the same VersioningContextServices helper so the two surfaces
    // are guaranteed to behave identically.
    // -----------------------------------------------------------------------

    private VersionedSaver<TEntity, TEntityId, TVersionId> Saver<TEntity, TEntityId, TVersionId>()
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningContextServices.Saver<TEntity, TEntityId, TVersionId>(this);

    private VersionedReader<TEntity, TEntityId, TVersionId> Reader<TEntity, TEntityId, TVersionId>()
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningContextServices.Reader<TEntity, TEntityId, TVersionId>(this);
}
