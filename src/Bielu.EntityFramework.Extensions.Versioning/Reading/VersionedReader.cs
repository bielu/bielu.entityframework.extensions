using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Bielu.EntityFramework.Extensions.Versioning.Reading;

/// <summary>
/// Internal read engine used by <see cref="VersionedDbContext"/> to implement
/// the read-side of the versioning API. Always uses
/// <see cref="EntityFrameworkQueryableExtensions.IgnoreQueryFilters{TEntity}(IQueryable{TEntity})"/>
/// so the soft-delete query filter does not hide tombstones from the reader's
/// own logic; the reader applies tombstone semantics explicitly in
/// <see cref="GetCurrentAsync(TEntityId, DateTimeOffset?, CancellationToken)"/>.
/// </summary>
internal sealed class VersionedReader<TEntity, TEntityId, TVersionId>(
    DbContext context,
    IVersioningClock clock)
    where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
    where TEntityId : notnull
    where TVersionId : notnull
{
    private DbSet<TEntity> Set => context.Set<TEntity>();

    public async Task<TEntity?> GetCurrentAsync(
        TEntityId entityId,
        DateTimeOffset? asOf,
        CancellationToken cancellationToken)
    {
        var asOfValue = asOf ?? clock.UtcNow;
        // Pick the most recent version <= asOf regardless of IsDeleted, then
        // honour tombstones by returning null if the latest version is a soft
        // delete. This is the correct "current view" semantics: a tombstone
        // does mark the aggregate as not-currently-present.
        // Tiebreaker for equal EffectiveAt is VersionId, matching the
        // (EntityId, EffectiveAt, VersionId) unique index.
        var latest = await Set
            .IgnoreQueryFilters()
            .Where(e => e.EntityId.Equals(entityId) && e.EffectiveAt <= asOfValue)
            .OrderByDescending(e => e.EffectiveAt)
            .ThenByDescending(e => e.VersionId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return latest is null || latest.IsDeleted ? null : latest;
    }

    public async Task<TEntity?> GetVersionAsync(TVersionId versionId, CancellationToken cancellationToken)
        => await Set
            .IgnoreQueryFilters()
            .Where(e => e.VersionId.Equals(versionId))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<TEntity>> GetAllVersionsAsync(
        TEntityId entityId,
        CancellationToken cancellationToken)
        => await Set
            .IgnoreQueryFilters()
            .Where(e => e.EntityId.Equals(entityId))
            .OrderBy(e => e.EffectiveAt)
            .ThenBy(e => e.VersionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<TEntity>> GetHistoryAsync(
        TEntityId entityId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
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
            .ThenBy(e => e.VersionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VersionNeighbors<TEntity>> GetNeighborsAsync(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken)
    {
        var previous = await Set
            .IgnoreQueryFilters()
            .Where(e => e.EntityId.Equals(entityId) && e.EffectiveAt <= effectiveAt)
            .OrderByDescending(e => e.EffectiveAt)
            .ThenByDescending(e => e.VersionId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var next = await Set
            .IgnoreQueryFilters()
            .Where(e => e.EntityId.Equals(entityId) && e.EffectiveAt > effectiveAt)
            .OrderBy(e => e.EffectiveAt)
            .ThenBy(e => e.VersionId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new VersionNeighbors<TEntity> { Previous = previous, Next = next };
    }

    public async Task<int> GetVersionCountAsync(TEntityId entityId, CancellationToken cancellationToken)
    {
        // COUNT is provably correct under all conditions; the (EntityId, …)
        // index makes it an index seek. We previously used MAX(VersionNumber)
        // here as a micro-optimisation, but that was only correct if
        // VersionNumber was a strict 1..N sequence per EntityId — which it
        // is not under concurrent inserts, since the interceptor stamps
        // VersionNumber from a non-locked MAX read. COUNT removes that
        // dependency entirely.
        return await Set
            .IgnoreQueryFilters()
            .Where(e => e.EntityId.Equals(entityId))
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
