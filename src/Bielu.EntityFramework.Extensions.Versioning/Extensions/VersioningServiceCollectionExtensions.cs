using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Internal;
using Bielu.EntityFramework.Extensions.Versioning.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bielu.EntityFramework.Extensions.Versioning.Extensions;

/// <summary>
/// DI extensions for registering the bielu content-versioning subsystem.
/// </summary>
public static class VersioningServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core versioning services (<see cref="VersioningOptions"/>,
    /// <see cref="IVersioningClock"/> and the
    /// <see cref="VersioningSaveChangesInterceptor"/>). Call this once per
    /// application; per-context wiring is automatic when the host
    /// <see cref="DbContext"/> is configured with
    /// <see cref="DbContextOptionsBuilder.UseApplicationServiceProvider"/>
    /// (the canonical pattern when using <c>AddDbContext</c>) — EF Core
    /// auto-discovers any registered <see cref="IInterceptor"/> from the
    /// application service provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional delegate to configure <see cref="VersioningOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddBieluVersioning(
        this IServiceCollection services,
        Action<VersioningOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            // Ensures IOptionsMonitor<VersioningOptions> is always resolvable.
            services.AddOptions<VersioningOptions>();
        }

        services.TryAddSingleton<IVersioningClock, SystemVersioningClock>();
        services.TryAddSingleton<VersioningSaveChangesInterceptor>();
        // Surface the interceptor as IInterceptor so EF Core picks it up
        // automatically from the application service provider.
        services.AddSingleton<IInterceptor>(sp =>
            sp.GetRequiredService<VersioningSaveChangesInterceptor>());
        return services;
    }

    /// <summary>
    /// Registers a typed <see cref="IVersionedRepository{TEntity, TEntityId, TVersionId}"/>
    /// backed by <typeparamref name="TContext"/> in the DI container.
    /// </summary>
    /// <typeparam name="TContext">The host <see cref="DbContext"/> type.</typeparam>
    /// <typeparam name="TEntity">The versioned entity type.</typeparam>
    /// <typeparam name="TEntityId">Aggregate identifier type.</typeparam>
    /// <typeparam name="TVersionId">Per-version identifier type.</typeparam>
    public static IServiceCollection AddVersionedEntity<TContext, TEntity, TEntityId, TVersionId>(
        this IServiceCollection services)
        where TContext : DbContext
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IVersionedRepository<TEntity, TEntityId, TVersionId>,
            VersionedRepository<TContext, TEntity, TEntityId, TVersionId>>();
        return services;
    }
}

/// <summary>
/// Extensions for wiring the versioning interceptor into a
/// <see cref="DbContextOptionsBuilder"/>.
/// </summary>
public static class VersioningDbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Adds the supplied <paramref name="interceptor"/> directly. Useful in
    /// tests where you want a custom or test-double interceptor without
    /// going through DI; in production, prefer
    /// <see cref="VersioningServiceCollectionExtensions.AddBieluVersioning(IServiceCollection, Action{VersioningOptions}?)"/>
    /// combined with
    /// <see cref="DbContextOptionsBuilder.UseApplicationServiceProvider"/>.
    /// </summary>
    public static DbContextOptionsBuilder UseBieluVersioning(
        this DbContextOptionsBuilder builder,
        VersioningSaveChangesInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(interceptor);
        builder.AddInterceptors(interceptor);
        return builder;
    }
}
