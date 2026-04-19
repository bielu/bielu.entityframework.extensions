using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Modeling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bielu.EntityFramework.Extensions.Versioning.ChangeTracking;

/// <summary>
/// EF Core <see cref="ISaveChangesInterceptor"/> that enforces the bielu
/// content-versioning invariants:
/// <list type="bullet">
///   <item>Stamps <c>RecordedAt</c> from <see cref="IVersioningClock"/>.</item>
///   <item>Rounds <c>EffectiveAt</c> per <see cref="VersioningOptions.EffectiveAtPrecision"/>.</item>
///   <item>Generates a <c>Guid</c>-based <c>VersionId</c> when none was provided
///         and the strategy permits it.</item>
///   <item>Computes a sequential, per-<c>EntityId</c> <c>VersionNumber</c> so that
///         the count of versions is available as a single index lookup.</item>
///   <item>Rejects in-place modifications of versioned entities (or converts
///         them to new versions) per <see cref="InPlaceUpdateBehavior"/>.</item>
///   <item>Detects <c>(EntityId, EffectiveAt)</c> collisions before the provider
///         raises an opaque unique-constraint error.</item>
/// </list>
/// </summary>
public sealed class VersioningSaveChangesInterceptor(
    IVersioningClock clock,
    IOptionsMonitor<VersioningOptions> optionsMonitor,
    ILogger<VersioningSaveChangesInterceptor> logger)
    : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
#pragma warning disable VSTHRD002 // EF's SavingChanges interceptor signature is synchronous; we must run the same logic here.
            ProcessChangesAsync(context, async: false, CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
            await ProcessChangesAsync(context, async: true, cancellationToken).ConfigureAwait(false);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessChangesAsync(DbContext context, bool async, CancellationToken cancellationToken)
    {
        var options = optionsMonitor.CurrentValue;
        var now = clock.UtcNow;

        // Snapshot tracked entries: stamping new versions may mutate the
        // change tracker (e.g. when InPlaceUpdateBehavior == ConvertToNewVersion).
        var entries = context.ChangeTracker.Entries().ToList();

        foreach (var entry in entries)
        {
            if (entry.Entity is not IVersionedEntity versioned)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    await StampAddedVersionAsync(context, entry, versioned, options, now, async, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case EntityState.Modified:
                    await HandleModifiedVersionAsync(context, entry, versioned, options, now, async, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                default:
                    break;
            }
        }
    }

    private async Task StampAddedVersionAsync(
        DbContext context,
        EntityEntry entry,
        IVersionedEntity versioned,
        VersioningOptions options,
        DateTimeOffset now,
        bool async,
        CancellationToken cancellationToken)
    {
        // RecordedAt is always stamped from the clock — never trust the caller.
        versioned.RecordedAt = now;

        // Round EffectiveAt to the configured precision.
        versioned.EffectiveAt = EffectiveAtRounding.Round(versioned.EffectiveAt, options.EffectiveAtPrecision);

        // Allocate a Guid-based VersionId if needed.
        AssignVersionIdIfNeeded(entry, options);

        var entityType = entry.Metadata.ClrType;
        var entityId = entry.Property("EntityId").CurrentValue
            ?? throw new InvalidOperationException(
                $"Versioned entity '{entityType.Name}' has no EntityId set.");
        var shape = VersionedQueryHelpers.GetShape(entityType);

        // Allocate the next sequential VersionNumber. Treat a zero value as
        // "unassigned"; this lets advanced consumers control numbering.
        if (versioned.VersionNumber <= 0)
        {
            versioned.VersionNumber = await ComputeNextVersionNumberAsync(
                context, shape, entityType, entityId, async, cancellationToken).ConfigureAwait(false);
        }

        // Detect collisions early when configured to do so.
        if (options.DetectCollisionsExplicitly && !options.AllowEffectiveAtTies)
        {
            await DetectCollisionAsync(
                context, shape, entityType, entry, entityId, versioned.EffectiveAt, async, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task HandleModifiedVersionAsync(
        DbContext context,
        EntityEntry entry,
        IVersionedEntity versioned,
        VersioningOptions options,
        DateTimeOffset now,
        bool async,
        CancellationToken cancellationToken)
    {
        switch (options.InPlaceUpdateBehavior)
        {
            case InPlaceUpdateBehavior.Throw:
                throw new InvalidOperationException(
                    $"In-place modification of versioned entity '{entry.Entity.GetType().Name}' is not allowed. " +
                    "Express modifications as new versions via VersionedDbContext.SaveAsync / UpdateAsync.");

            case InPlaceUpdateBehavior.ConvertToNewVersion:
            {
                var clone = entry.CurrentValues.Clone().ToObject();
                entry.State = EntityState.Unchanged;

                if (clone is IVersionedEntity clonedVersioned)
                {
                    ResetVersionIdForClone(clone, options);
                    clonedVersioned.RecordedAt = now;
                    clonedVersioned.EffectiveAt = EffectiveAtRounding.Round(
                        clonedVersioned.EffectiveAt == default ? now : clonedVersioned.EffectiveAt,
                        options.EffectiveAtPrecision);
                    clonedVersioned.VersionNumber = 0;

                    var addedEntry = context.Add(clone);
                    AssignVersionIdIfNeeded(addedEntry, options);

                    var entityType = addedEntry.Metadata.ClrType;
                    var entityId = addedEntry.Property("EntityId").CurrentValue
                        ?? throw new InvalidOperationException(
                            $"Versioned entity '{entityType.Name}' has no EntityId set.");
                    var shape = VersionedQueryHelpers.GetShape(entityType);

                    clonedVersioned.VersionNumber = await ComputeNextVersionNumberAsync(
                        context, shape, entityType, entityId, async, cancellationToken).ConfigureAwait(false);

                    if (options.DetectCollisionsExplicitly && !options.AllowEffectiveAtTies)
                    {
                        await DetectCollisionAsync(
                            context, shape, entityType, addedEntry, entityId,
                            clonedVersioned.EffectiveAt, async, cancellationToken).ConfigureAwait(false);
                    }
                }
                break;
            }

            case InPlaceUpdateBehavior.Allow:
                versioned.RecordedAt = now;
                break;

            default:
                break;
        }
    }

    private static void AssignVersionIdIfNeeded(EntityEntry entry, VersioningOptions options)
    {
        if (options.VersionIdStrategy != VersionIdStrategy.NewGuid)
        {
            return;
        }

        var versionIdProperty = entry.Property("VersionId");
        if (versionIdProperty.CurrentValue is Guid g && g == Guid.Empty)
        {
            versionIdProperty.CurrentValue = Guid.NewGuid();
        }
    }

    private static void ResetVersionIdForClone(object clone, VersioningOptions options)
    {
        if (options.VersionIdStrategy != VersionIdStrategy.NewGuid)
        {
            return;
        }

        var prop = clone.GetType().GetProperty("VersionId");
        if (prop is null || !prop.CanWrite)
        {
            return;
        }

        if (prop.PropertyType == typeof(Guid))
        {
            prop.SetValue(clone, Guid.NewGuid());
        }
    }

    private static async Task<int> ComputeNextVersionNumberAsync(
        DbContext context,
        IVersionedEntityShape shape,
        Type entityType,
        object entityId,
        bool async,
        CancellationToken cancellationToken)
    {
        var dbMax = await shape.GetMaxVersionNumberAsync(context, entityId, async, cancellationToken)
            .ConfigureAwait(false);

        // Highest VersionNumber for this EntityId already assigned to another
        // Added entry in the current change-set — covers multiple SaveAsync
        // calls before SaveChangesAsync.
        var trackedMax = context.ChangeTracker
            .Entries()
            .Where(e => e.State == EntityState.Added
                        && entityType.IsInstanceOfType(e.Entity)
                        && e.Entity is IVersionedEntity)
            .Select(e =>
            {
                var trackedEntityId = e.Property("EntityId").CurrentValue;
                if (!Equals(trackedEntityId, entityId))
                {
                    return 0;
                }

                return ((IVersionedEntity)e.Entity).VersionNumber;
            })
            .DefaultIfEmpty(0)
            .Max();

        var current = Math.Max(dbMax ?? 0, trackedMax);
        return current + 1;
    }

    private async Task DetectCollisionAsync(
        DbContext context,
        IVersionedEntityShape shape,
        Type entityType,
        EntityEntry entry,
        object entityId,
        DateTimeOffset effectiveAt,
        bool async,
        CancellationToken cancellationToken)
    {
        var versionId = entry.Property("VersionId").CurrentValue;

        var existsInDb = await shape.AnyEffectiveAtCollisionAsync(
            context, entityId, versionId, effectiveAt, async, cancellationToken).ConfigureAwait(false);

        var existsInChangeTracker = context.ChangeTracker
            .Entries()
            .Any(e => e.State == EntityState.Added
                      && !ReferenceEquals(e.Entity, entry.Entity)
                      && entityType.IsInstanceOfType(e.Entity)
                      && e.Entity is IVersionedEntity other
                      && other.EffectiveAt == effectiveAt
                      && Equals(e.Property("EntityId").CurrentValue, entityId));

        if (existsInDb || existsInChangeTracker)
        {
            logger.LogWarning(
                "EffectiveAt collision detected for EntityId={EntityId} at {EffectiveAt} on {Entity}.",
                entityId, effectiveAt, entityType.Name);
            throw new EffectiveAtCollisionException(entityId, effectiveAt);
        }
    }
}
