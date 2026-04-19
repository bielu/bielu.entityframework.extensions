using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Internal;
using Bielu.EntityFramework.Extensions.Versioning.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bielu.EntityFramework.Extensions.Versioning.Extensions;

/// <summary>
/// Extension methods that expose the bielu content-versioning
/// <c>Save</c>/<c>Update</c> entry points directly on
/// <see cref="DbContext"/> and <see cref="DbSet{TEntity}"/>, so that consumers
/// do not have to inject <see cref="IVersionedRepository{TEntity, TEntityId, TVersionId}"/>
/// to use them.
/// </summary>
/// <remarks>
/// All extension methods resolve the <see cref="IVersioningClock"/> and
/// <see cref="IOptionsMonitor{TOptions}"/> from the
/// <see cref="DbContext.GetService{TService}"/> / internal service provider of
/// the host context. <c>AddBieluVersioning</c> registers them automatically;
/// for tests, an in-process <see cref="ServiceProvider"/> wired through
/// <see cref="DbContextOptionsBuilder.UseApplicationServiceProvider"/> works
/// equally well.
/// </remarks>
public static class VersioningDbContextExtensions
{
    // -----------------------------------------------------------------------
    // Save / SaveAsync (upsert: works on new or existing aggregates)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Saves <paramref name="payload"/> as a new version for
    /// <paramref name="entityId"/>. Equivalent to
    /// <see cref="IVersionedRepository{TEntity, TEntityId, TVersionId}.SaveAsync"/>.
    /// </summary>
    public static Task<VersionSaveResult<TEntity>> SaveAsync<TEntity, TEntityId, TVersionId>(
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
        return RepositoryFor<TEntity, TEntityId, TVersionId>(context)
            .SaveAsync(entityId, effectiveAt, payload, cancellationToken);
    }

    /// <summary>
    /// Synchronous counterpart to
    /// <see cref="SaveAsync{TEntity, TEntityId, TVersionId}"/>.
    /// </summary>
    public static VersionSaveResult<TEntity> Save<TEntity, TEntityId, TVersionId>(
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
        return RepositoryFor<TEntity, TEntityId, TVersionId>(context).Save(entityId, effectiveAt, payload);
    }

    /// <summary>
    /// Saves <paramref name="payload"/> as a new version directly through the
    /// <see cref="DbSet{TEntity}"/>.
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
        => GetContext(set).SaveAsync<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, payload, cancellationToken);

    /// <summary>
    /// Synchronous counterpart to
    /// <see cref="SaveAsync{TEntity, TEntityId, TVersionId}(DbSet{TEntity}, TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.
    /// </summary>
    public static VersionSaveResult<TEntity> Save<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => GetContext(set).Save<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, payload);

    // -----------------------------------------------------------------------
    // Update / UpdateAsync (requires the aggregate to already exist)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Persists <paramref name="payload"/> as a new version of an existing
    /// aggregate. Equivalent to
    /// <see cref="IVersionedRepository{TEntity, TEntityId, TVersionId}.UpdateAsync"/>.
    /// </summary>
    public static Task<VersionSaveResult<TEntity>> UpdateAsync<TEntity, TEntityId, TVersionId>(
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
        return RepositoryFor<TEntity, TEntityId, TVersionId>(context)
            .UpdateAsync(entityId, effectiveAt, payload, cancellationToken);
    }

    /// <summary>Synchronous counterpart to
    /// <see cref="UpdateAsync{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public static VersionSaveResult<TEntity> Update<TEntity, TEntityId, TVersionId>(
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
        return RepositoryFor<TEntity, TEntityId, TVersionId>(context).Update(entityId, effectiveAt, payload);
    }

    /// <summary>DbSet variant of
    /// <see cref="UpdateAsync{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, DateTimeOffset, TEntity, CancellationToken)"/>.</summary>
    public static Task<VersionSaveResult<TEntity>> UpdateAsync<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => GetContext(set).UpdateAsync<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, payload, cancellationToken);

    /// <summary>DbSet variant of
    /// <see cref="Update{TEntity, TEntityId, TVersionId}(DbContext, TEntityId, DateTimeOffset, TEntity)"/>.</summary>
    public static VersionSaveResult<TEntity> Update<TEntity, TEntityId, TVersionId>(
        this DbSet<TEntity> set,
        TEntityId entityId,
        DateTimeOffset effectiveAt,
        TEntity payload)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => GetContext(set).Update<TEntity, TEntityId, TVersionId>(entityId, effectiveAt, payload);

    // -----------------------------------------------------------------------
    // Read helpers (the cheap ones the user explicitly asked for)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the version current as of <paramref name="asOf"/> for
    /// <paramref name="entityId"/>.
    /// </summary>
    public static Task<TEntity?> GetCurrentVersionAsync<TEntity, TEntityId, TVersionId>(
        this DbContext context,
        TEntityId entityId,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        return RepositoryFor<TEntity, TEntityId, TVersionId>(context)
            .GetCurrentAsync(entityId, asOf, cancellationToken);
    }

    /// <summary>
    /// Returns every version of <paramref name="entityId"/>, ordered by
    /// <c>EffectiveAt</c> ascending.
    /// </summary>
    public static Task<IReadOnlyList<TEntity>> GetAllVersionsAsync<TEntity, TEntityId, TVersionId>(
        this DbContext context,
        TEntityId entityId,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        return RepositoryFor<TEntity, TEntityId, TVersionId>(context)
            .GetAllVersionsAsync(entityId, cancellationToken);
    }

    /// <summary>
    /// Returns the total number of versions recorded for
    /// <paramref name="entityId"/> as a single index lookup.
    /// </summary>
    public static Task<int> GetVersionCountAsync<TEntity, TEntityId, TVersionId>(
        this DbContext context,
        TEntityId entityId,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        return RepositoryFor<TEntity, TEntityId, TVersionId>(context)
            .GetVersionCountAsync(entityId, cancellationToken);
    }

    // -----------------------------------------------------------------------
    // Internal plumbing (also used by the bulk + base-context companions)
    // -----------------------------------------------------------------------

    internal static IVersionedRepository<TEntity, TEntityId, TVersionId> RepositoryForVersioning<TEntity, TEntityId, TVersionId>(
        this DbContext context)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        var clock = ResolveOrDefault<IVersioningClock>(context, () => new SystemVersioningClock());
        var optionsMonitor = ResolveOrDefault<IOptionsMonitor<VersioningOptions>>(
            context, () => new StaticOptionsMonitor<VersioningOptions>(new VersioningOptions()));

        // We use a non-generic DbContext-typed repository here: the
        // repository implementation only needs the abstract DbContext
        // contract, so this avoids forcing callers to thread their concrete
        // TContext type through every extension method.
        return new VersionedRepository<DbContext, TEntity, TEntityId, TVersionId>(context, clock, optionsMonitor);
    }

    private static IVersionedRepository<TEntity, TEntityId, TVersionId> RepositoryFor<TEntity, TEntityId, TVersionId>(
        DbContext context)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => context.RepositoryForVersioning<TEntity, TEntityId, TVersionId>();

    private static T ResolveOrDefault<T>(DbContext context, Func<T> fallback) where T : class
    {
        // The application service provider (set via UseApplicationServiceProvider)
        // is the canonical way to wire DI services into EF Core, but we also
        // tolerate a fully unregistered scenario for unit tests that talk to
        // the repository directly.
        var infrastructure = context.GetInfrastructure();
        var resolved = infrastructure.GetService(typeof(T)) as T;
        if (resolved is not null)
        {
            return resolved;
        }

        var appServices = (infrastructure.GetService(typeof(IDbContextOptions)) as IDbContextOptions)
            ?.Extensions.OfType<CoreOptionsExtension>().FirstOrDefault()?.ApplicationServiceProvider;
        return appServices?.GetService(typeof(T)) as T ?? fallback();
    }

    internal static DbContext GetVersioningContext<TEntity>(this DbSet<TEntity> set) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(set);
        return set.GetService<ICurrentDbContext>().Context;
    }

    private static DbContext GetContext<TEntity>(DbSet<TEntity> set) where TEntity : class
        => set.GetVersioningContext();
}

/// <summary>
/// Trivial <see cref="IOptionsMonitor{TOptions}"/> wrapping a fixed value;
/// used as a fallback when the DI container has no real registration. Internal
/// because consumers should always go through <c>AddBieluVersioning</c>.
/// </summary>
internal sealed class StaticOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
{
    public TOptions CurrentValue { get; } = value;

    public TOptions Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}
