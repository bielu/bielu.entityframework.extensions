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
        => SaveSingleAsync(entityId, effectiveAt, payload, requireExisting: false, cancellationToken);

    /// <inheritdoc />
    public VersionSaveResult<TEntity> Save(TEntityId entityId, DateTimeOffset effectiveAt, TEntity payload)
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => SaveSingleAsync(entityId, effectiveAt, payload, requireExisting: false, CancellationToken.None)
            .GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    /// <inheritdoc />
    public Task<IReadOnlyList<VersionSaveResult<TEntity>>> SaveManyAsync(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        => SaveManyCoreAsync(requests, requireExisting: false, cancellationToken);

    /// <inheritdoc />
    public IReadOnlyList<VersionSaveResult<TEntity>> SaveMany(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => SaveManyCoreAsync(requests, requireExisting: false, CancellationToken.None)
            .GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    /// <inheritdoc />
    public Task<VersionSaveResult<TEntity>> UpdateAsync(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        => SaveSingleAsync(entityId, effectiveAt, payload, requireExisting: true, cancellationToken);

    /// <inheritdoc />
    public VersionSaveResult<TEntity> Update(TEntityId entityId, DateTimeOffset effectiveAt, TEntity payload)
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => SaveSingleAsync(entityId, effectiveAt, payload, requireExisting: true, CancellationToken.None)
            .GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    /// <inheritdoc />
    public Task<IReadOnlyList<VersionSaveResult<TEntity>>> UpdateManyAsync(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        => SaveManyCoreAsync(requests, requireExisting: true, cancellationToken);

    /// <inheritdoc />
    public IReadOnlyList<VersionSaveResult<TEntity>> UpdateMany(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => SaveManyCoreAsync(requests, requireExisting: true, CancellationToken.None)
            .GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    /// <inheritdoc />
    public Task<VersionSaveResult<TEntity>> UpsertAsync(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        // Upsert == Save: classification (Initial / Current / Archive) is
        // already exposed on the result so the caller can tell whether they
        // effectively created the aggregate.
        => SaveSingleAsync(entityId, effectiveAt, payload, requireExisting: false, cancellationToken);

    /// <inheritdoc />
    public VersionSaveResult<TEntity> Upsert(TEntityId entityId, DateTimeOffset effectiveAt, TEntity payload)
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => SaveSingleAsync(entityId, effectiveAt, payload, requireExisting: false, CancellationToken.None)
            .GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    /// <inheritdoc />
    public Task<IReadOnlyList<VersionSaveResult<TEntity>>> UpsertManyAsync(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        => SaveManyCoreAsync(requests, requireExisting: false, cancellationToken);

    /// <inheritdoc />
    public IReadOnlyList<VersionSaveResult<TEntity>> UpsertMany(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => SaveManyCoreAsync(requests, requireExisting: false, CancellationToken.None)
            .GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    private async Task<VersionSaveResult<TEntity>> SaveSingleAsync(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        bool requireExisting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var request = new VersionWriteRequest<TEntity, TEntityId>(entityId, effectiveAt, payload);
        var results = await SaveManyCoreAsync(new[] { request }, requireExisting, cancellationToken)
            .ConfigureAwait(false);
        return results[0];
    }

    private async Task<IReadOnlyList<VersionSaveResult<TEntity>>> SaveManyCoreAsync(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        bool requireExisting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var materialised = requests as IReadOnlyList<VersionWriteRequest<TEntity, TEntityId>>
                           ?? requests.ToList();
        if (materialised.Count == 0)
        {
            return Array.Empty<VersionSaveResult<TEntity>>();
        }

        var shape = VersionedQueryHelpers.GetShape(typeof(TEntity));
        var precision = Options.EffectiveAtPrecision;

        // Group by EntityId so we issue a single MAX(EffectiveAt) lookup per
        // aggregate even when a batch contains many writes for the same one.
        var existingMaxByEntityId = new Dictionary<TEntityId, DateTimeOffset?>();
        foreach (var request in materialised)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Payload);

            if (existingMaxByEntityId.ContainsKey(request.EntityId))
            {
                continue;
            }

            var max = await shape.GetMaxEffectiveAtAsync(Context, request.EntityId, async: true, cancellationToken)
                .ConfigureAwait(false);
            existingMaxByEntityId[request.EntityId] = max;
        }

        if (requireExisting)
        {
            foreach (var (entityId, max) in existingMaxByEntityId)
            {
                if (max is null)
                {
                    throw new InvalidOperationException(
                        $"Cannot Update aggregate '{entityId}': no existing versions found. Use SaveAsync/UpsertAsync to create the first version.");
                }
            }
        }

        var results = new List<VersionSaveResult<TEntity>>(materialised.Count);
        // Track the running maximum per EntityId so that the second write in a
        // batch is classified relative to the first write, not the unchanged
        // database state.
        var runningMax = new Dictionary<TEntityId, DateTimeOffset?>(existingMaxByEntityId);

        foreach (var request in materialised)
        {
            var roundedEffectiveAt = EffectiveAtRounding.Round(request.EffectiveAt, precision);
            request.Payload.EntityId = request.EntityId;
            request.Payload.EffectiveAt = roundedEffectiveAt;

            var currentMax = runningMax[request.EntityId];
            var kind = ClassifyKind(roundedEffectiveAt, currentMax);
            if (currentMax is null || roundedEffectiveAt > currentMax.Value)
            {
                runningMax[request.EntityId] = roundedEffectiveAt;
            }

            Set.Add(request.Payload);
            results.Add(new VersionSaveResult<TEntity>(request.Payload, kind));
        }

        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return results;
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
        return SaveSingleAsync(entityId, effectiveAt, tombstonePayload, requireExisting: false, cancellationToken);
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
