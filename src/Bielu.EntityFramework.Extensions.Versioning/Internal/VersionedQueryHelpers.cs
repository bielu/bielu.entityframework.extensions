using System.Collections.Concurrent;
using System.Reflection;
using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Bielu.EntityFramework.Extensions.Versioning.Internal;

/// <summary>
/// Builds and caches strongly-typed LINQ expressions used by the interceptor
/// and the repository. Centralising the expression construction keeps EF
/// expression trees provider-agnostic (no <c>dynamic</c> or interface casts).
/// </summary>
internal static class VersionedQueryHelpers
{
    private static readonly ConcurrentDictionary<Type, IVersionedEntityShape> Shapes = new();

    public static IVersionedEntityShape GetShape(Type entityType)
    {
        return Shapes.GetOrAdd(entityType, t => (IVersionedEntityShape)Activator.CreateInstance(
            typeof(VersionedEntityShape<,,>).MakeGenericType(t, GetEntityIdType(t), GetVersionIdType(t)))!);
    }

    private static Type GetEntityIdType(Type entityType)
    {
        var iface = FindVersionedInterface(entityType);
        return iface.GetGenericArguments()[0];
    }

    private static Type GetVersionIdType(Type entityType)
    {
        var iface = FindVersionedInterface(entityType);
        return iface.GetGenericArguments()[1];
    }

    private static Type FindVersionedInterface(Type entityType)
    {
        var iface = entityType.GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVersionedEntity<,>))
            ?? throw new InvalidOperationException(
                $"Type '{entityType.FullName}' does not implement IVersionedEntity<TEntityId, TVersionId>.");
        return iface;
    }
}

internal interface IVersionedEntityShape
{
    Task<int?> GetMaxVersionNumberAsync(DbContext context, object entityId, bool async, CancellationToken ct);

    Task<DateTimeOffset?> GetMaxEffectiveAtAsync(DbContext context, object entityId, bool async, CancellationToken ct);

    Task<bool> AnyEffectiveAtCollisionAsync(
        DbContext context, object entityId, object? excludeVersionId, DateTimeOffset effectiveAt, bool async, CancellationToken ct);
}

internal sealed class VersionedEntityShape<TEntity, TEntityId, TVersionId> : IVersionedEntityShape
    where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
    where TEntityId : notnull
    where TVersionId : notnull
{
    public async Task<int?> GetMaxVersionNumberAsync(
        DbContext context, object entityId, bool async, CancellationToken ct)
    {
        var typedId = (TEntityId)entityId;
        var query = context.Set<TEntity>()
            .IgnoreQueryFilters()
            .Where(e => e.EntityId.Equals(typedId))
            .Select(e => (int?)e.VersionNumber);

        return async
            ? await query.MaxAsync(ct).ConfigureAwait(false)
            : query.Max();
    }

    public async Task<DateTimeOffset?> GetMaxEffectiveAtAsync(
        DbContext context, object entityId, bool async, CancellationToken ct)
    {
        var typedId = (TEntityId)entityId;
        var query = context.Set<TEntity>()
            .IgnoreQueryFilters()
            .Where(e => e.EntityId.Equals(typedId))
            .Select(e => (DateTimeOffset?)e.EffectiveAt);

        return async
            ? await query.MaxAsync(ct).ConfigureAwait(false)
            : query.Max();
    }

    public async Task<bool> AnyEffectiveAtCollisionAsync(
        DbContext context, object entityId, object? excludeVersionId, DateTimeOffset effectiveAt,
        bool async, CancellationToken ct)
    {
        var typedId = (TEntityId)entityId;
        IQueryable<TEntity> query = context.Set<TEntity>()
            .IgnoreQueryFilters()
            .Where(e => e.EffectiveAt == effectiveAt && e.EntityId.Equals(typedId));

        if (excludeVersionId is TVersionId typedVersionId)
        {
            query = query.Where(e => !e.VersionId.Equals(typedVersionId));
        }

        return async
            ? await query.AnyAsync(ct).ConfigureAwait(false)
            : query.Any();
    }
}
