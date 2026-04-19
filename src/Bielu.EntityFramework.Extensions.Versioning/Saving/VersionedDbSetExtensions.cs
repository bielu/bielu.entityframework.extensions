using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Registration;
using Microsoft.EntityFrameworkCore;

namespace Bielu.EntityFramework.Extensions.Versioning.Saving;

/// <summary>
/// Versioning write entry points exposed as extension methods on
/// <see cref="DbSet{TEntity}"/>. Discoverable only on sets whose element type
/// satisfies the <see cref="IVersionedEntity{TEntityId, TVersionId}"/>
/// constraint, so non-versioned <see cref="DbSet{TEntity}"/>s remain
/// unaffected.
/// </summary>
/// <remarks>
/// <para>
/// These extensions delegate to the same internal save engine used by
/// <see cref="VersionedDbContext"/>; both surfaces are guaranteed to behave
/// identically. Use them when your context derives from a plain
/// <see cref="DbContext"/> rather than <see cref="VersionedDbContext"/>, or
/// when the call-site reads more naturally as
/// <c>db.Contents.SaveAsync(...)</c> than as <c>db.SaveAsync&lt;Content,…&gt;(...)</c>.
/// </para>
/// </remarks>
public static class VersionedDbSetSaveExtensions
{
    // -----------------------------------------------------------------------
    // Save (upsert) — single
    // -----------------------------------------------------------------------

    /// <summary>
    /// Persists <paramref name="payload"/> as a new version row. Equivalent
    /// to <see cref="VersionedDbContext.SaveAsync{TEntity, TEntityId, TVersionId}(TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.
    /// </summary>
    public static Task<VersionSaveResult<TEntity>> SaveAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(payload);
        return VersioningContextServices
            .Saver<TEntity, TEntityId, TVersionId>(set.ContextOf())
            .SaveSingleAsync(entityId, effectiveAt, payload, requireExisting: false, cancellationToken);
    }

    /// <summary>Synchronous counterpart to
    /// <see cref="SaveAsync{TEntity, TEntityId, TVersionId}(DbSet{TEntity}, TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public static VersionSaveResult<TEntity> Save<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => set.SaveAsync<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, payload).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    /// <summary>Bulk variant of
    /// <see cref="SaveAsync{TEntity, TEntityId, TVersionId}(DbSet{TEntity}, TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public static Task<IReadOnlyList<VersionSaveResult<TEntity>>> SaveManyAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(requests);
        return VersioningContextServices
            .Saver<TEntity, TEntityId, TVersionId>(set.ContextOf())
            .SaveManyCoreAsync(requests, requireExisting: false, cancellationToken);
    }

    /// <summary>Synchronous counterpart to
    /// <see cref="SaveManyAsync{TEntity, TEntityId, TVersionId}(DbSet{TEntity}, IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)"/>.</summary>
    public static IReadOnlyList<VersionSaveResult<TEntity>> SaveMany<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => set.SaveManyAsync<TEntity, TEntityId, TVersionId>(requests).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    // -----------------------------------------------------------------------
    // Update — single & many (require an existing aggregate)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Persists <paramref name="payload"/> as a new version of an existing
    /// aggregate; throws if the aggregate has no prior versions.
    /// </summary>
    public static Task<VersionSaveResult<TEntity>> UpdateAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(payload);
        return VersioningContextServices
            .Saver<TEntity, TEntityId, TVersionId>(set.ContextOf())
            .SaveSingleAsync(entityId, effectiveAt, payload, requireExisting: true, cancellationToken);
    }

    /// <summary>Synchronous counterpart to
    /// <see cref="UpdateAsync{TEntity, TEntityId, TVersionId}(DbSet{TEntity}, TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public static VersionSaveResult<TEntity> Update<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => set.UpdateAsync<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, payload).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    /// <summary>Bulk variant of
    /// <see cref="UpdateAsync{TEntity, TEntityId, TVersionId}(DbSet{TEntity}, TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public static Task<IReadOnlyList<VersionSaveResult<TEntity>>> UpdateManyAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(requests);
        return VersioningContextServices
            .Saver<TEntity, TEntityId, TVersionId>(set.ContextOf())
            .SaveManyCoreAsync(requests, requireExisting: true, cancellationToken);
    }

    /// <summary>Synchronous counterpart to
    /// <see cref="UpdateManyAsync{TEntity, TEntityId, TVersionId}(DbSet{TEntity}, IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)"/>.</summary>
    public static IReadOnlyList<VersionSaveResult<TEntity>> UpdateMany<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
#pragma warning disable VSTHRD002 // Synchronous wrapper for the documented sync API.
        => set.UpdateManyAsync<TEntity, TEntityId, TVersionId>(requests).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    // -----------------------------------------------------------------------
    // Upsert — single & many (alias of Save; reads more clearly at call-sites)
    // -----------------------------------------------------------------------

    /// <summary>Create-or-update entry point — equivalent to
    /// <see cref="SaveAsync{TEntity, TEntityId, TVersionId}(DbSet{TEntity}, TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public static Task<VersionSaveResult<TEntity>> UpsertAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => set.SaveAsync<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, payload, cancellationToken);

    /// <summary>Synchronous counterpart to
    /// <see cref="UpsertAsync{TEntity, TEntityId, TVersionId}(DbSet{TEntity}, TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public static VersionSaveResult<TEntity> Upsert<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => set.Save<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, payload);

    /// <summary>Bulk variant of
    /// <see cref="UpsertAsync{TEntity, TEntityId, TVersionId}(DbSet{TEntity}, TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public static Task<IReadOnlyList<VersionSaveResult<TEntity>>> UpsertManyAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => set.SaveManyAsync<TEntity, TEntityId, TVersionId>(requests, cancellationToken);

    /// <summary>Synchronous counterpart to
    /// <see cref="UpsertManyAsync{TEntity, TEntityId, TVersionId}(DbSet{TEntity}, IEnumerable{VersionWriteRequest{TEntity, TEntityId}}, CancellationToken)"/>.</summary>
    public static IReadOnlyList<VersionSaveResult<TEntity>> UpsertMany<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        IEnumerable<VersionWriteRequest<TEntity, TEntityId>> requests)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => set.SaveMany<TEntity, TEntityId, TVersionId>(requests);

    // -----------------------------------------------------------------------
    // Soft delete & hard remove
    // -----------------------------------------------------------------------

    /// <summary>
    /// Adds a tombstone version (i.e. a version with
    /// <see cref="IVersionedEntity.IsDeleted"/> set to <see langword="true"/>)
    /// at the given <paramref name="effectiveAt"/>.
    /// </summary>
    public static Task<VersionSaveResult<TEntity>> SoftDeleteVersionAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
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
        return set.SaveAsync<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, tombstonePayload, cancellationToken);
    }

    /// <summary>
    /// Hard-removes a single version row from the database. Use sparingly —
    /// removing a version breaks the immutable-history invariant.
    /// </summary>
    public static async Task RemoveVersionAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TVersionId versionId,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(set);
        var existing = await set
            .IgnoreQueryFilters()
            .Where(e => e.VersionId.Equals(versionId))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return;
        }

        var ctx = set.ContextOf();
        set.Remove(existing);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
