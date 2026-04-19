using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Extensions;
using Bielu.EntityFramework.Extensions.Versioning.Repository;
using Microsoft.EntityFrameworkCore;

namespace Bielu.EntityFramework.Extensions.Versioning;

/// <summary>
/// Optional <see cref="DbContext"/> base class that surfaces the bielu
/// versioning <c>Save</c> / <c>Update</c> / <c>Upsert</c> entry points (and
/// their <c>Many</c> / async siblings) directly on the context, so consumers
/// can write <c>db.SaveAsync(entityId, effectiveAt, content)</c> without
/// having to thread an <see cref="IVersionedRepository{TEntity, TEntityId, TVersionId}"/>
/// through their domain code.
/// </summary>
/// <remarks>
/// Deriving from <see cref="VersionedDbContext"/> is purely a convenience —
/// every method here forwards to the equivalent extension method on
/// <see cref="DbContext"/>, so consumers who prefer composition over
/// inheritance can keep using the extension methods directly. The base class
/// is sealed against accidental shadowing of EF's own <see cref="DbContext"/>
/// methods: nothing is overridden, only added.
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
    // Save / SaveAsync
    // -----------------------------------------------------------------------

    /// <inheritdoc cref="VersioningDbContextExtensions.SaveAsync{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, DateTimeOffset, TEntity, CancellationToken)" />
    public Task<VersionSaveResult<TEntity>> SaveAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextExtensions.SaveAsync<TEntity, TEntityId, TVersionId>(this, entityId, effectiveAt, payload, cancellationToken);

    /// <inheritdoc cref="VersioningDbContextExtensions.Save{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, DateTimeOffset, TEntity)" />
    public VersionSaveResult<TEntity> Save<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextExtensions.Save<TEntity, TEntityId, TVersionId>(this, entityId, effectiveAt, payload);

    /// <inheritdoc cref="VersioningDbContextBulkExtensions.SaveManyAsync{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)" />
    public Task<IReadOnlyList<VersionSaveResult<TEntity>>> SaveManyAsync<TEntity, TEntityId, TVersionId>(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextBulkExtensions.SaveManyAsync<TEntity, TEntityId, TVersionId>(this, requests, cancellationToken);

    /// <inheritdoc cref="VersioningDbContextBulkExtensions.SaveMany{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}})" />
    public IReadOnlyList<VersionSaveResult<TEntity>> SaveMany<TEntity, TEntityId, TVersionId>(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextBulkExtensions.SaveMany<TEntity, TEntityId, TVersionId>(this, requests);

    // -----------------------------------------------------------------------
    // Update / UpdateAsync
    // -----------------------------------------------------------------------

    /// <inheritdoc cref="VersioningDbContextExtensions.UpdateAsync{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, DateTimeOffset, TEntity, CancellationToken)" />
    public Task<VersionSaveResult<TEntity>> UpdateAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextExtensions.UpdateAsync<TEntity, TEntityId, TVersionId>(this, entityId, effectiveAt, payload, cancellationToken);

    /// <inheritdoc cref="VersioningDbContextExtensions.Update{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, DateTimeOffset, TEntity)" />
    public VersionSaveResult<TEntity> Update<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextExtensions.Update<TEntity, TEntityId, TVersionId>(this, entityId, effectiveAt, payload);

    /// <inheritdoc cref="VersioningDbContextBulkExtensions.UpdateManyAsync{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)" />
    public Task<IReadOnlyList<VersionSaveResult<TEntity>>> UpdateManyAsync<TEntity, TEntityId, TVersionId>(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextBulkExtensions.UpdateManyAsync<TEntity, TEntityId, TVersionId>(this, requests, cancellationToken);

    /// <inheritdoc cref="VersioningDbContextBulkExtensions.UpdateMany{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}})" />
    public IReadOnlyList<VersionSaveResult<TEntity>> UpdateMany<TEntity, TEntityId, TVersionId>(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextBulkExtensions.UpdateMany<TEntity, TEntityId, TVersionId>(this, requests);

    // -----------------------------------------------------------------------
    // Upsert / UpsertAsync (single & many)
    // -----------------------------------------------------------------------

    /// <inheritdoc cref="VersioningDbContextBulkExtensions.UpsertAsync{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, DateTimeOffset, TEntity, CancellationToken)" />
    public Task<VersionSaveResult<TEntity>> UpsertAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextBulkExtensions.UpsertAsync<TEntity, TEntityId, TVersionId>(this, entityId, effectiveAt, payload, cancellationToken);

    /// <inheritdoc cref="VersioningDbContextBulkExtensions.Upsert{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, DateTimeOffset, TEntity)" />
    public VersionSaveResult<TEntity> Upsert<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextBulkExtensions.Upsert<TEntity, TEntityId, TVersionId>(this, entityId, effectiveAt, payload);

    /// <inheritdoc cref="VersioningDbContextBulkExtensions.UpsertManyAsync{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)" />
    public Task<IReadOnlyList<VersionSaveResult<TEntity>>> UpsertManyAsync<TEntity, TEntityId, TVersionId>(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextBulkExtensions.UpsertManyAsync<TEntity, TEntityId, TVersionId>(this, requests, cancellationToken);

    /// <inheritdoc cref="VersioningDbContextBulkExtensions.UpsertMany{TEntity, TEntityId, TVersionId}(DbContext, IEnumerable{VersionWriteRequest{TEntity, TEntityId}})" />
    public IReadOnlyList<VersionSaveResult<TEntity>> UpsertMany<TEntity, TEntityId, TVersionId>(
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextBulkExtensions.UpsertMany<TEntity, TEntityId, TVersionId>(this, requests);

    // -----------------------------------------------------------------------
    // Read helpers
    // -----------------------------------------------------------------------

    /// <inheritdoc cref="VersioningDbContextExtensions.GetCurrentVersionAsync{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, DateTimeOffset?, CancellationToken)" />
    public Task<TEntity?> GetCurrentVersionAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextExtensions.GetCurrentVersionAsync<TEntity, TEntityId, TVersionId>(this, entityId, asOf, cancellationToken);

    /// <inheritdoc cref="VersioningDbContextExtensions.GetAllVersionsAsync{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, CancellationToken)" />
    public Task<IReadOnlyList<TEntity>> GetAllVersionsAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextExtensions.GetAllVersionsAsync<TEntity, TEntityId, TVersionId>(this, entityId, cancellationToken);

    /// <inheritdoc cref="VersioningDbContextExtensions.GetVersionCountAsync{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, CancellationToken)" />
    public Task<int> GetVersionCountAsync<TEntity, TEntityId, TVersionId>(
        TEntityId entityId,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => VersioningDbContextExtensions.GetVersionCountAsync<TEntity, TEntityId, TVersionId>(this, entityId, cancellationToken);
}
