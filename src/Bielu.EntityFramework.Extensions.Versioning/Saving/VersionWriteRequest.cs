namespace Bielu.EntityFramework.Extensions.Versioning.Saving;

/// <summary>
/// A request to persist a single version, used by the bulk
/// <c>SaveMany</c> / <c>UpdateMany</c> / <c>UpsertMany</c> entry points on
/// <see cref="VersionedDbContext"/>.
/// </summary>
/// <typeparam name="TEntity">The versioned entity type.</typeparam>
/// <typeparam name="TEntityId">Aggregate identifier type.</typeparam>
/// <param name="EntityId">The aggregate identifier.</param>
/// <param name="EffectiveAt">The business-time at which the version becomes effective.</param>
/// <param name="Payload">The payload to persist as the new version.</param>
public sealed record VersionWriteRequest<TEntity, TEntityId>(
    TEntityId EntityId,
    DateTimeOffset EffectiveAt,
    TEntity Payload)
    where TEntity : class
    where TEntityId : notnull;
