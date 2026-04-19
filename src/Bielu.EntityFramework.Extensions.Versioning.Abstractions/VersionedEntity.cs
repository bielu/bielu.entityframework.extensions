namespace Bielu.EntityFramework.Extensions.Versioning.Abstractions;

/// <summary>
/// Convenience base class implementing <see cref="IVersionedEntity{TEntityId,TVersionId}"/>.
/// Provides sensible defaults: <see cref="RecordedAt"/> is initialised to
/// <see cref="DateTimeOffset.UtcNow"/>, and <see cref="VersionId"/> is
/// initialised to <see cref="Guid.NewGuid"/> when <typeparamref name="TVersionId"/>
/// is <see cref="Guid"/>.
/// </summary>
/// <typeparam name="TEntityId">Aggregate identifier type.</typeparam>
/// <typeparam name="TVersionId">Per-version identifier type.</typeparam>
public abstract class VersionedEntity<TEntityId, TVersionId> : IVersionedEntity<TEntityId, TVersionId>
    where TEntityId : notnull
    where TVersionId : notnull
{
    /// <inheritdoc />
    public TEntityId EntityId { get; set; } = default!;

    /// <inheritdoc />
    public TVersionId VersionId { get; set; } = CreateDefaultVersionId();

    /// <inheritdoc />
    public DateTimeOffset EffectiveAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public int VersionNumber { get; set; }

    /// <summary>
    /// Optional concurrency token. Mapped as a concurrency token via EF Core's
    /// <c>IsConcurrencyToken()</c> so each provider materialises it idiomatically;
    /// no provider-specific row-version is assumed.
    /// </summary>
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    private static TVersionId CreateDefaultVersionId()
    {
        if (typeof(TVersionId) == typeof(Guid))
        {
            return (TVersionId)(object)Guid.NewGuid();
        }

        return default!;
    }
}
