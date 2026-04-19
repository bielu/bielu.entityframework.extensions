using System.Linq.Expressions;
using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bielu.EntityFramework.Extensions.Versioning.Modeling;

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
        builder.Property(e => e.RecordedAt).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired();
        builder.Property(e => e.VersionNumber).IsRequired();

        // EffectiveAt is stored as a long (binary encoding of DateTimeOffset)
        // for portable aggregation: SQLite (and some other providers) cannot
        // apply MAX/MIN to native DateTimeOffset values, so we round-trip
        // through DateTimeOffsetToBinaryConverter — which is part of the core
        // EF Core package and works on every provider.
        builder.Property(e => e.EffectiveAt)
               .IsRequired()
               .HasConversion(new DateTimeOffsetToBinaryConverter());

        // Composite unique index on (EntityId, EffectiveAt, VersionId): the
        // VersionId tiebreaker means two writes that legitimately share the
        // same EffectiveAt do not clash on this index, while ordering remains
        // deterministic. Whether ties are *allowed* by the application is
        // governed by VersioningOptions.AllowEffectiveAtTies and enforced by
        // the SaveChangesInterceptor.
        builder.HasIndex(nameof(IVersionedEntity<TEntityId, TVersionId>.EntityId),
                         nameof(IVersionedEntity.EffectiveAt),
                         nameof(IVersionedEntity<TEntityId, TVersionId>.VersionId))
               .IsUnique();

        // Helper index for "current version" lookups: (EntityId, EffectiveAt).
        builder.HasIndex(nameof(IVersionedEntity<TEntityId, TVersionId>.EntityId),
                         nameof(IVersionedEntity.EffectiveAt));

        // Insertion-order index: keeps an ordered scan by VersionNumber per
        // aggregate cheap. The highest VersionNumber for an EntityId is a
        // best-effort indicator of "how many versions" — under concurrent
        // inserts there can be gaps/duplicates because the interceptor
        // stamps VersionNumber from a non-locked MAX read; consumers needing
        // the actual count should use COUNT (which the reader does).
        builder.HasIndex(nameof(IVersionedEntity<TEntityId, TVersionId>.EntityId),
                         nameof(IVersionedEntity.VersionNumber));

        // Concurrency token (when present on the runtime type). We only opt
        // it in when the property is actually defined on the CLR type so that
        // entities not deriving from VersionedEntity<,> are not penalised.
        var concurrencyTokenProperty = typeof(TEntity).GetProperty("ConcurrencyToken");
        if (concurrencyTokenProperty is not null && concurrencyTokenProperty.PropertyType == typeof(Guid))
        {
            builder.Property("ConcurrencyToken").IsConcurrencyToken();
        }

        // Optional global query filter that hides soft-delete tombstones.
        // This is intentionally NOT a "latest version per EntityId" filter:
        // restricting a result set to the most-recent version per EntityId
        // requires a self-correlated query that EF Core cannot translate
        // uniformly across providers. Use VersionedDbContext.GetCurrentAsync
        // / DbSet<T>.GetCurrentAsync (or an explicit GroupBy LINQ expression)
        // for "current view" semantics.
        if (options.QueryFilterBehavior == QueryFilterBehavior.HideTombstones)
        {
            Expression<Func<TEntity, bool>> filter = e => !e.IsDeleted;
            builder.HasQueryFilter(filter);
        }
    }
}
