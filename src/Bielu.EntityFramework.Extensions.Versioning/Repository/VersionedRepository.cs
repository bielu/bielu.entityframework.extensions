using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bielu.EntityFramework.Extensions.Versioning.Repository;

/// <summary>
/// Default <see cref="IVersionedRepository{TEntity, TEntityId, TVersionId}"/>
/// implementation backed by EF Core. Uses only standard EF building blocks
/// (<see cref="DbSet{TEntity}"/>, LINQ, <c>IgnoreQueryFilters</c>) so it works
/// against every relational and non-relational provider.
/// </summary>
/// <typeparam name="TContext">The host <see cref="DbContext"/> type.</typeparam>
/// <typeparam name="TEntity">The versioned entity type.</typeparam>
/// <typeparam name="TEntityId">Aggregate identifier type.</typeparam>
/// <typeparam name="TVersionId">Per-version identifier type.</typeparam>
public class VersionedRepository<TContext, TEntity, TEntityId, TVersionId>(
    TContext context,
    IVersioningClock clock,
    IOptionsMonitor<VersioningOptions> optionsMonitor)
    : IVersionedRepository<TEntity, TEntityId, TVersionId>
    where TContext : DbContext
    where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
    where TEntityId : notnull
    where TVersionId : notnull
{
    /// <summary>The underlying <see cref="DbContext"/>.</summary>
    protected TContext Context { get; } = context;

    /// <summary>The clock abstraction used to resolve "now".</summary>
    protected IVersioningClock Clock { get; } = clock;

    /// <summary>The current versioning options.</summary>
    protected VersioningOptions Options => optionsMonitor.CurrentValue;

    private DbSet<TEntity> Set => Context.Set<TEntity>();

    /// <inheritdoc />
    public Task<VersionSaveResult<TEntity>> SaveAsync(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        => SaveCoreAsync(entityId, effectiveAt, payload, requireExisting: false, cancellationToken);

    /// <inheritdoc />
    public VersionSaveResult<TEntity> Save(TEntityId entityId, DateTimeOffset effectiveAt, TEntity payload)
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => SaveCoreAsync(entityId, effectiveAt, payload, requireExisting: false, CancellationToken.None)
            .GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    /// <inheritdoc />
    public Task<VersionSaveResult<TEntity>> UpdateAsync(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        => SaveCoreAsync(entityId, effectiveAt, payload, requireExisting: true, cancellationToken);

    /// <inheritdoc />
    public VersionSaveResult<TEntity> Update(TEntityId entityId, DateTimeOffset effectiveAt, TEntity payload)
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => SaveCoreAsync(entityId, effectiveAt, payload, requireExisting: true, CancellationToken.None)
            .GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    private async Task<VersionSaveResult<TEntity>> SaveCoreAsync(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        bool requireExisting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // Stamp the supplied payload with the aggregate id and effective time
        // so that the SaveChangesInterceptor sees consistent values when it
        // runs. We do not assign VersionId / RecordedAt / VersionNumber here —
        // those are the interceptor's responsibility.
        payload.EntityId = entityId;
        payload.EffectiveAt = EffectiveAtRounding.Round(effectiveAt, Options.EffectiveAtPrecision);

        // Classify the new row by comparing against the current MAX(EffectiveAt).
        var shape = VersionedQueryHelpers.GetShape(typeof(TEntity));
        var maxExisting = await shape.GetMaxEffectiveAtAsync(Context, entityId, async: true, cancellationToken)
            .ConfigureAwait(false);

        if (requireExisting && maxExisting is null)
        {
            throw new InvalidOperationException(
                $"Cannot Update aggregate '{entityId}': no existing versions found. Use SaveAsync to create the first version.");
        }

        var kind = ClassifyKind(payload.EffectiveAt, maxExisting);

        Set.Add(payload);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new VersionSaveResult<TEntity>(payload, kind);
    }

    private static VersionKind ClassifyKind(DateTimeOffset newEffectiveAt, DateTimeOffset? existingMax)
    {
        if (existingMax is null)
        {
            return VersionKind.Initial;
        }

        return newEffectiveAt >= existingMax.Value
            ? VersionKind.Current
            : VersionKind.Archive;
    }

    /// <inheritdoc />
    public async Task<TEntity?> GetCurrentAsync(
        TEntityId entityId,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var asOfValue = asOf ?? Clock.UtcNow;
        // Pick the most recent version <= asOf regardless of IsDeleted, then
        // honour tombstones by returning null if the latest version is a soft
        // delete. This is the correct "current view" semantics: a tombstone
        // does mark the aggregate as not-currently-present.
        var latest = await Set
            .IgnoreQueryFilters()
            .Where(e => e.EntityId.Equals(entityId) && e.EffectiveAt <= asOfValue)
            .OrderByDescending(e => e.EffectiveAt)
            .ThenByDescending(e => e.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return latest is null || latest.IsDeleted ? null : latest;
    }

    /// <inheritdoc />
    public async Task<TEntity?> GetVersionAsync(TVersionId versionId, CancellationToken cancellationToken = default)
        => await Set
            .IgnoreQueryFilters()
            .Where(e => e.VersionId.Equals(versionId))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TEntity>> GetAllVersionsAsync(
        TEntityId entityId,
        CancellationToken cancellationToken = default)
        => await Set
            .IgnoreQueryFilters()
            .Where(e => e.EntityId.Equals(entityId))
            .OrderBy(e => e.EffectiveAt)
            .ThenBy(e => e.VersionNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TEntity>> GetHistoryAsync(
        TEntityId entityId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> query = Set.IgnoreQueryFilters().Where(e => e.EntityId.Equals(entityId));
        if (from is { } fromValue)
        {
            query = query.Where(e => e.EffectiveAt >= fromValue);
        }

        if (to is { } toValue)
        {
            query = query.Where(e => e.EffectiveAt <= toValue);
        }

        return await query
            .OrderBy(e => e.EffectiveAt)
            .ThenBy(e => e.VersionNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<VersionNeighbors<TEntity>> GetNeighborsAsync(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken = default)
    {
        var previous = await Set
            .IgnoreQueryFilters()
            .Where(e => e.EntityId.Equals(entityId) && e.EffectiveAt <= effectiveAt)
            .OrderByDescending(e => e.EffectiveAt)
            .ThenByDescending(e => e.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var next = await Set
            .IgnoreQueryFilters()
            .Where(e => e.EntityId.Equals(entityId) && e.EffectiveAt > effectiveAt)
            .OrderBy(e => e.EffectiveAt)
            .ThenBy(e => e.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new VersionNeighbors<TEntity> { Previous = previous, Next = next };
    }

    /// <inheritdoc />
    public async Task<int> GetVersionCountAsync(TEntityId entityId, CancellationToken cancellationToken = default)
    {
        // MAX(VersionNumber) is an index seek given IX_*_EntityId_VersionNumber.
        // The highest insertion-order number for an aggregate equals the
        // total number of versions.
        var max = await Set
            .IgnoreQueryFilters()
            .Where(e => e.EntityId.Equals(entityId))
            .Select(e => (int?)e.VersionNumber)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);

        return max ?? 0;
    }

    /// <inheritdoc />
    public Task<VersionSaveResult<TEntity>> SoftDeleteVersionAsync(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity tombstonePayload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tombstonePayload);
        tombstonePayload.IsDeleted = true;
        return SaveCoreAsync(entityId, effectiveAt, tombstonePayload, requireExisting: false, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveVersionAsync(TVersionId versionId, CancellationToken cancellationToken = default)
    {
        var existing = await Set
            .IgnoreQueryFilters()
            .Where(e => e.VersionId.Equals(versionId))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return;
        }

        // Bypass the "no in-place modifications" guard with a delete: deletes
        // are handled separately by the interceptor (we never touch them).
        Set.Remove(existing);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
