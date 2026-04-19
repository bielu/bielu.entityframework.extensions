using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Reading;
using Bielu.EntityFramework.Extensions.Versioning.Saving;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;

namespace Bielu.EntityFramework.Extensions.Versioning.Registration;

/// <summary>
/// Internal service-resolution helpers shared between
/// <see cref="VersionedDbContext"/> and the <see cref="DbSet{TEntity}"/>
/// extension methods. Centralising the logic ensures both surfaces honour
/// <see cref="DbContextOptionsBuilder.UseApplicationServiceProvider"/> exactly
/// the same way and fall back to the same defaults in unconfigured / test
/// scenarios.
/// </summary>
internal static class VersioningContextServices
{
    public static IVersioningClock ResolveClock(DbContext context)
        => ResolveOrDefault<IVersioningClock>(context, static () => new SystemVersioningClock());

    public static VersioningOptions ResolveOptions(DbContext context)
        => ResolveOrDefault<IOptionsMonitor<VersioningOptions>>(
               context,
               static () => new StaticOptionsMonitor<VersioningOptions>(new VersioningOptions()))
           .CurrentValue;

    public static VersionedSaver<TEntity, TEntityId, TVersionId> Saver<TEntity, TEntityId, TVersionId>(
        DbContext context)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => new(context, ResolveOptions(context));

    public static VersionedReader<TEntity, TEntityId, TVersionId> Reader<TEntity, TEntityId, TVersionId>(
        DbContext context)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
        => new(context, ResolveClock(context));

    /// <summary>
    /// Resolves the parent <see cref="DbContext"/> for a <see cref="DbSet{TEntity}"/>
    /// via the EF Core <see cref="ICurrentDbContext"/> service, so the
    /// <c>DbSet</c> extension methods can reach the surrounding context's
    /// service provider, change tracker and <c>SaveChangesAsync</c>.
    /// </summary>
    public static DbContext ContextOf<TEntity>(this DbSet<TEntity> set) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(set);
        return set.GetService<ICurrentDbContext>().Context;
    }

    private static T ResolveOrDefault<T>(DbContext context, Func<T> fallback) where T : class
    {
        // The application service provider (set via UseApplicationServiceProvider)
        // is the canonical way to wire DI services into EF Core, but we also
        // tolerate a fully unregistered scenario for unit tests that construct
        // a context directly without DI.
        var infrastructure = context.GetInfrastructure();
        if (infrastructure.GetService(typeof(T)) is T resolved)
        {
            return resolved;
        }

        var appServices = (infrastructure.GetService(typeof(IDbContextOptions)) as IDbContextOptions)
            ?.Extensions.OfType<CoreOptionsExtension>().FirstOrDefault()?.ApplicationServiceProvider;
        return appServices?.GetService(typeof(T)) as T ?? fallback();
    }

    /// <summary>
    /// Trivial <see cref="IOptionsMonitor{TOptions}"/> wrapping a fixed value;
    /// used as a fallback when the DI container has no real registration.
    /// Returns a no-op disposable from <see cref="OnChange"/> to satisfy the
    /// interface contract — callers commonly dispose the returned token
    /// unconditionally.
    /// </summary>
    private sealed class StaticOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue { get; } = value;

        public TOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<TOptions, string?> listener) => NoOpDisposable.Instance;

        private sealed class NoOpDisposable : IDisposable
        {
            public static readonly NoOpDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
