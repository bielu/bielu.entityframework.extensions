namespace Bielu.EntityFramework.Extensions.Versioning.Abstractions;

/// <summary>
/// Marker interface implemented by all versioned entities.
/// </summary>
/// <remarks>
/// This non-generic interface allows infrastructure code (interceptors, model
/// customizers, etc.) to detect versioned entities without having to know the
/// concrete <typeparamref name="TEntityId"/> / <typeparamref name="TVersionId"/>
/// types. Use <see cref="IVersionedEntity{TEntityId,TVersionId}"/> in domain code.
/// </remarks>
public interface IVersionedEntity
{
    /// <summary>
    /// Business time at which this version becomes effective.
    /// Versions are ordered by this value; new versions can be inserted between
    /// existing ones simply by choosing an <see cref="EffectiveAt"/> that falls
    /// between two existing versions.
    /// </summary>
    DateTimeOffset EffectiveAt { get; set; }

    /// <summary>
    /// Wall-clock time at which this version was recorded by the system.
    /// Stamped automatically by the versioning save-changes interceptor; for
    /// late-arriving updates it will be greater than <see cref="EffectiveAt"/>.
    /// </summary>
    DateTimeOffset RecordedAt { get; set; }

    /// <summary>
    /// Logical-delete flag for this version. A new tombstone version is the
    /// recommended way to "delete" content while preserving history.
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// Insertion-order sequence number assigned per <c>EntityId</c>, starting
    /// at <c>1</c> for the first version that is ever recorded for the
    /// aggregate and incremented by <c>1</c> on every subsequent
    /// <c>AddVersionAsync</c> call (regardless of <see cref="EffectiveAt"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="VersionNumber"/> is intentionally <b>independent</b> of
    /// <see cref="EffectiveAt"/>: chronological ordering of the timeline is
    /// always done via <see cref="EffectiveAt"/>; <see cref="VersionNumber"/>
    /// captures the order in which versions were <i>recorded</i>. This lets a
    /// late-arriving update be inserted between two existing versions without
    /// renumbering anything: it just receives the next available
    /// <see cref="VersionNumber"/>.
    /// </para>
    /// <para>
    /// Because the highest <see cref="VersionNumber"/> for a given
    /// <c>EntityId</c> equals the total number of versions, the count is
    /// available as a single index lookup (<c>MAX(VersionNumber)</c>) and is
    /// also free of any further query whenever the caller already has a
    /// version row in hand — typically the one returned by
    /// <c>GetCurrentAsync</c>.
    /// </para>
    /// </remarks>
    int VersionNumber { get; set; }
}

/// <summary>
/// Strongly-typed contract for an entity that participates in content
/// versioning. The aggregate as a whole is identified by
/// <see cref="EntityId"/>; each individual version row is identified by its
/// own dedicated <see cref="VersionId"/>.
/// </summary>
/// <typeparam name="TEntityId">
/// Type of the stable, shared aggregate identifier (e.g. <see cref="Guid"/>,
/// <see cref="long"/>, <see cref="string"/>).
/// </typeparam>
/// <typeparam name="TVersionId">
/// Type of the dedicated per-version identifier. Typically <see cref="Guid"/>
/// for distributed assignment, but any EF-supported key type is accepted.
/// </typeparam>
public interface IVersionedEntity<TEntityId, TVersionId> : IVersionedEntity
    where TEntityId : notnull
    where TVersionId : notnull
{
    /// <summary>
    /// Stable identifier of the aggregate this version belongs to. All
    /// versions of the same content share the same value.
    /// </summary>
    TEntityId EntityId { get; set; }

    /// <summary>
    /// Dedicated primary-key identifier of this version row.
    /// </summary>
    TVersionId VersionId { get; set; }
}
