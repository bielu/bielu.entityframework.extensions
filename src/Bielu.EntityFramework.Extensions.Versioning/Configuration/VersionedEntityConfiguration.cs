using System.Linq.Expressions;
using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bielu.EntityFramework.Extensions.Versioning.Configuration;

/// <summary>
/// Re-usable <see cref="IEntityTypeConfiguration{TEntity}"/> implementation that
/// applies the conventions required by the bielu content-versioning extension
/// to a given versioned entity type.
/// </summary>
/// <typeparam name="TEntity">The CLR type of the versioned entity.</typeparam>
/// <typeparam name="TEntityId">Aggregate identifier type.</typeparam>
/// <typeparam name="TVersionId">Per-version identifier type.</typeparam>
public sealed class VersionedEntityConfiguration<TEntity, TEntityId, TVersionId>(VersioningOptions options)
    : IEntityTypeConfiguration<TEntity>
    where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
    where TEntityId : notnull
    where TVersionId : notnull
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Dedicated per-row primary key.
        builder.HasKey(e => e.VersionId);

        // Required properties.
        builder.Property(e => e.EntityId).IsRequired();
        builder.Property(e => e.VersionId).IsRequired();
        builder.Property(e => e.EffectiveAt).IsRequired();
        builder.Property(e => e.RecordedAt).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired();
        builder.Property(e => e.VersionNumber).IsRequired();

        // Composite unique index on (EntityId, EffectiveAt, VersionId): the
        // VersionId tiebreaker means two writes that legitimately share the
        // same EffectiveAt do not clash on this index, while ordering remains
        // deterministic. Whether ties are *allowed* by the application is
        // governed by VersioningOptions.AllowEffectiveAtTies and enforced by
        // the SaveChangesInterceptor.
        builder.HasIndex(nameof(IVersionedEntity<TEntityId, TVersionId>.EntityId),
                         nameof(IVersionedEntity.EffectiveAt),
                         nameof(IVersionedEntity<TEntityId, TVersionId>.VersionId))
               .IsUnique()
               .HasDatabaseName($"IX_{typeof(TEntity).Name}_EntityId_EffectiveAt_VersionId");

        // Helper index for "current version" lookups: (EntityId, EffectiveAt).
        builder.HasIndex(nameof(IVersionedEntity<TEntityId, TVersionId>.EntityId),
                         nameof(IVersionedEntity.EffectiveAt))
               .HasDatabaseName($"IX_{typeof(TEntity).Name}_EntityId_EffectiveAt");

        // Insertion-order index: makes MAX(VersionNumber) per EntityId an
        // index seek. The highest VersionNumber for an EntityId equals the
        // total number of versions for that aggregate, so callers get the
        // count for free.
        builder.HasIndex(nameof(IVersionedEntity<TEntityId, TVersionId>.EntityId),
                         nameof(IVersionedEntity.VersionNumber))
               .HasDatabaseName($"IX_{typeof(TEntity).Name}_EntityId_VersionNumber");

        // Concurrency token (when present on the runtime type). We only opt
        // it in when the property is actually defined on the CLR type so that
        // entities not deriving from VersionedEntity<,> are not penalised.
        var concurrencyTokenProperty = typeof(TEntity).GetProperty("ConcurrencyToken");
        if (concurrencyTokenProperty is not null && concurrencyTokenProperty.PropertyType == typeof(Guid))
        {
            builder.Property("ConcurrencyToken").IsConcurrencyToken();
        }

        // Optional global query filter for "current non-deleted version" only.
        if (options.QueryFilterBehavior == QueryFilterBehavior.AsOfNow)
        {
            // Per-EntityId latest-non-deleted is not safely expressible as a
            // simple HasQueryFilter (it requires a self-correlated query that
            // EF Core cannot translate uniformly across providers). We
            // install a soft-delete filter; the VersionedRepository exposes
            // "current as-of" semantics via LINQ.
            Expression<Func<TEntity, bool>> filter = e => !e.IsDeleted;
            builder.HasQueryFilter(filter);
        }
    }
}
