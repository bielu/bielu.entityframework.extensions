using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Modeling;
using Microsoft.EntityFrameworkCore;

namespace Bielu.EntityFramework.Extensions.Versioning.Saving;

/// <summary>
/// Internal save engine used by <see cref="VersionedDbContext"/> to implement
/// the <c>Save</c> / <c>Update</c> / <c>Upsert</c> entry points (and their
/// <c>Many</c> / async siblings) on top of plain <see cref="DbSet{TEntity}"/>
/// operations.
/// </summary>
/// <remarks>
/// All write operations share the same core: classify each request relative to
/// the existing <c>MAX(EffectiveAt)</c> per <c>EntityId</c>, attach the
/// payload, and let the registered <c>VersioningSaveChangesInterceptor</c>
/// stamp the version metadata before
/// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> flushes the
/// change-set in a single transaction (on relational providers).
/// </remarks>
internal sealed class VersionedSaver<TEntity, TEntityId, TVersionId>(
    DbContext context,
    VersioningOptions options)
    where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
    where TEntityId : notnull
    where TVersionId : notnull
{
    private DbSet<TEntity> Set => context.Set<TEntity>();

    public async Task<VersionSaveResult<TEntity>> SaveSingleAsync(
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

    public async Task<IReadOnlyList<VersionSaveResult<TEntity>>> SaveManyCoreAsync(
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
        var precision = options.EffectiveAtPrecision;

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

            var max = await shape.GetMaxEffectiveAtAsync(context, request.EntityId, async: true, cancellationToken)
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

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
}
