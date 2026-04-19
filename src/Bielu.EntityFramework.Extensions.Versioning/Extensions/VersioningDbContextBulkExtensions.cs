using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Repository;
using Microsoft.EntityFrameworkCore;

namespace Bielu.EntityFramework.Extensions.Versioning.Extensions;

/// <summary>
/// Bulk and upsert variants of the <c>Save</c> / <c>Update</c> versioning
/// entry points exposed on <see cref="DbContext"/> and
/// <see cref="DbSet{TEntity}"/>.
/// </summary>
public static class VersioningDbContextBulkExtensions
{
    // -----------------------------------------------------------------------
    // SaveMany / SaveManyAsync
    // -----------------------------------------------------------------------

    /// <summary>Bulk variant of <c>SaveAsync</c>.</summary>
    public static Task<IReadOnlyList<VersionSaveResult<TEntity>>> SaveManyAsync<TEntity, TEntityId, TVersionId>(
        this DbContext context,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requests);
        return context.RepositoryForVersioning<TEntity, TEntityId, TVersionId>()
            .SaveManyAsync(requests, cancellationToken);
    }

    /// <summary>Synchronous counterpart to <see cref="SaveManyAsync{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)"/>.</summary>
    public static IReadOnlyList<VersionSaveResult<TEntity>> SaveMany<TEntity, TEntityId, TVersionId>(
        this DbContext context,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requests);
        return context.RepositoryForVersioning<TEntity, TEntityId, TVersionId>().SaveMany(requests);
    }

    /// <summary>DbSet variant of <see cref="SaveManyAsync{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)"/>.</summary>
    public static Task<IReadOnlyList<VersionSaveResult<TEntity>>> SaveManyAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => set.GetVersioningContext().SaveManyAsync<TEntity, TEntityId, TVersionId>(requests, cancellationToken);

    /// <summary>DbSet variant of <see cref="SaveMany{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}})"/>.</summary>
    public static IReadOnlyList<VersionSaveResult<TEntity>> SaveMany<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => set.GetVersioningContext().SaveMany<TEntity, TEntityId, TVersionId>(requests);

    // -----------------------------------------------------------------------
    // UpdateMany / UpdateManyAsync
    // -----------------------------------------------------------------------

    /// <summary>Bulk variant of <c>UpdateAsync</c>.</summary>
    public static Task<IReadOnlyList<VersionSaveResult<TEntity>>> UpdateManyAsync<TEntity, TEntityId, TVersionId>(
        this DbContext context,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requests);
        return context.RepositoryForVersioning<TEntity, TEntityId, TVersionId>()
            .UpdateManyAsync(requests, cancellationToken);
    }

    /// <summary>Synchronous counterpart to <see cref="UpdateManyAsync{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)"/>.</summary>
    public static IReadOnlyList<VersionSaveResult<TEntity>> UpdateMany<TEntity, TEntityId, TVersionId>(
        this DbContext context,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requests);
        return context.RepositoryForVersioning<TEntity, TEntityId, TVersionId>().UpdateMany(requests);
    }

    /// <summary>DbSet variant of <see cref="UpdateManyAsync{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)"/>.</summary>
    public static Task<IReadOnlyList<VersionSaveResult<TEntity>>> UpdateManyAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => set.GetVersioningContext().UpdateManyAsync<TEntity, TEntityId, TVersionId>(requests, cancellationToken);

    /// <summary>DbSet variant of <see cref="UpdateMany{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}})"/>.</summary>
    public static IReadOnlyList<VersionSaveResult<TEntity>> UpdateMany<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => set.GetVersioningContext().UpdateMany<TEntity, TEntityId, TVersionId>(requests);

    // -----------------------------------------------------------------------
    // Upsert / UpsertAsync (single)
    // -----------------------------------------------------------------------

    /// <summary>Create-or-update entry point. See <see cref="IVersionedRepository{TEntity, TEntityId, TVersionId}.UpsertAsync"/>.</summary>
    public static Task<VersionSaveResult<TEntity>> UpsertAsync<TEntity, TEntityId, TVersionId>(
        this DbContext context,
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(payload);
        return context.RepositoryForVersioning<TEntity, TEntityId, TVersionId>()
            .UpsertAsync(entityId, effectiveAt, payload, cancellationToken);
    }

    /// <summary>Synchronous counterpart to <see cref="UpsertAsync{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public static VersionSaveResult<TEntity> Upsert<TEntity, TEntityId, TVersionId>(
        this DbContext context,
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(payload);
        return context.RepositoryForVersioning<TEntity, TEntityId, TVersionId>()
            .Upsert(entityId, effectiveAt, payload);
    }

    /// <summary>DbSet variant of <see cref="UpsertAsync{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public static Task<VersionSaveResult<TEntity>> UpsertAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => set.GetVersioningContext().UpsertAsync<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, payload, cancellationToken);

    /// <summary>DbSet variant of <see cref="Upsert{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, DateTimeOffset, TEntity)"/>.</summary>
    public static VersionSaveResult<TEntity> Upsert<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => set.GetVersioningContext().Upsert<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, payload);

    // -----------------------------------------------------------------------
    // UpsertMany / UpsertManyAsync
    // -----------------------------------------------------------------------

    /// <summary>Bulk variant of <c>UpsertAsync</c>.</summary>
    public static Task<IReadOnlyList<VersionSaveResult<TEntity>>> UpsertManyAsync<TEntity, TEntityId, TVersionId>(
        this DbContext context,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requests);
        return context.RepositoryForVersioning<TEntity, TEntityId, TVersionId>()
            .UpsertManyAsync(requests, cancellationToken);
    }

    /// <summary>Synchronous counterpart to <see cref="UpsertManyAsync{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)"/>.</summary>
    public static IReadOnlyList<VersionSaveResult<TEntity>> UpsertMany<TEntity, TEntityId, TVersionId>(
        this DbContext context,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requests);
        return context.RepositoryForVersioning<TEntity, TEntityId, TVersionId>().UpsertMany(requests);
    }

    /// <summary>DbSet variant of <see cref="UpsertManyAsync{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)"/>.</summary>
    public static Task<IReadOnlyList<VersionSaveResult<TEntity>>> UpsertManyAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => set.GetVersioningContext().UpsertManyAsync<TEntity, TEntityId, TVersionId>(requests, cancellationToken);

    /// <summary>DbSet variant of <see cref="UpsertMany{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}})"/>.</summary>
    public static IReadOnlyList<VersionSaveResult<TEntity>> UpsertMany<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => set.GetVersioningContext().UpsertMany<TEntity, TEntityId, TVersionId>(requests);
}
