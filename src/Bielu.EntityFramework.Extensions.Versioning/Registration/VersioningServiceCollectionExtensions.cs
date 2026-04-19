using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Bielu.EntityFramework.Extensions.Versioning.Registration;

/// <summary>
/// DI extensions for registering the bielu content-versioning subsystem and
/// for wiring a <see cref="VersionedDbContext"/>-derived context with all the
/// plumbing it needs (application service provider + interceptor) in a single
/// call.
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
        // Logging is a hard dependency of the interceptor; register a no-op
        // implementation if the host application has not wired logging itself
        // so that AddBieluVersioning() is sufficient on its own.
        services.AddLogging();
        services.TryAddSingleton<VersioningSaveChangesInterceptor>();
        // Surface the interceptor as IInterceptor so EF Core picks it up
        // automatically from the application service provider.
        services.AddSingleton<IInterceptor>(sp =>
            sp.GetRequiredService<VersioningSaveChangesInterceptor>());
        return services;
    }

    /// <summary>
    /// Registers a <see cref="VersionedDbContext"/>-derived context together
    /// with all the plumbing it needs to participate in the bielu versioning
    /// subsystem: <see cref="AddBieluVersioning"/> is invoked, the application
    /// service provider is wired through
    /// <see cref="DbContextOptionsBuilder.UseApplicationServiceProvider"/>,
    /// and the <see cref="VersioningSaveChangesInterceptor"/> is added — in a
    /// single call.
    /// </summary>
    /// <typeparam name="TContext">
    /// The concrete <see cref="VersionedDbContext"/> derivative to register.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsAction">
    /// Caller-supplied configuration callback, typically used to call
    /// <c>UseSqlite</c>/<c>UseSqlServer</c>/etc. The infrastructure plumbing
    /// (<see cref="DbContextOptionsBuilder.UseApplicationServiceProvider"/>
    /// and <see cref="VersioningSaveChangesInterceptor"/>) is applied first,
    /// then this delegate runs so consumers can override or layer additional
    /// configuration.
    /// </param>
    /// <param name="contextLifetime">Service lifetime for the context.</param>
    /// <param name="optionsLifetime">Service lifetime for the options object.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddVersionedDbContext<TContext>(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder>? optionsAction = null,
        ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
        ServiceLifetime optionsLifetime = ServiceLifetime.Scoped)
        where TContext : VersionedDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddBieluVersioning();
        services.AddDbContext<TContext>(
            (sp, options) =>
            {
                options.UseApplicationServiceProvider(sp);
                options.AddInterceptors(sp.GetRequiredService<VersioningSaveChangesInterceptor>());
                optionsAction?.Invoke(sp, options);
            },
            contextLifetime,
            optionsLifetime);
        return services;
    }
}

/// <summary>
/// Extensions for wiring the versioning interceptor into a
/// <see cref="DbContextOptionsBuilder"/> directly, for scenarios where DI is
/// not available (e.g. unit tests).
/// </summary>
public static class VersioningDbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Adds the supplied <paramref name="interceptor"/> directly. Useful in
    /// tests where you want a custom or test-double interceptor without
    /// going through DI; in production, prefer
    /// <see cref="VersioningServiceCollectionExtensions.AddVersionedDbContext{TContext}(IServiceCollection, Action{IServiceProvider, DbContextOptionsBuilder}?, ServiceLifetime, ServiceLifetime)"/>.
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
