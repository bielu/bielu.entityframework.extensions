namespace Bielu.EntityFramework.Extensions.Versioning.Abstractions;

/// <summary>
/// Strategy used to assign a <c>VersionId</c> to a newly added version row
/// when the caller has not supplied one.
/// </summary>
public enum VersionIdStrategy
{
    /// <summary>
    /// Assign <see cref="Guid.NewGuid"/> for <see cref="Guid"/> keys; for any
    /// other key type the caller must supply the value.
    /// </summary>
    NewGuid = 0,

    /// <summary>
    /// Always require the caller to provide the version id; the framework will
    /// not generate one.
    /// </summary>
    CallerProvided = 1,
}

/// <summary>
/// Behaviour of the global query filter installed on versioned entities.
/// </summary>
public enum QueryFilterBehavior
{
    /// <summary>
    /// Do not install a global query filter. Consumers see every version row
    /// returned by their queries and are responsible for selecting the version
    /// they need (this is the safest default for analytical workloads).
    /// </summary>
    AllVersions = 0,

    /// <summary>
    /// Install a global query filter that hides soft-deleted versions
    /// (rows with <see cref="IVersionedEntity.IsDeleted"/> set to
    /// <see langword="true"/>). Use <c>IgnoreQueryFilters()</c> on the query
    /// to opt out per-call.
    /// <para>
    /// Note: this filter is intentionally <b>not</b> a "latest-version-only"
    /// filter. Restricting a result set to the most-recent version per
    /// <c>EntityId</c> requires a self-correlated query that EF Core cannot
    /// translate uniformly across providers; use the
    /// <c>VersionedDbContext.GetCurrentAsync</c> /
    /// <c>DbSet&lt;T&gt;.GetCurrentAsync</c> APIs (or an explicit
    /// <c>GroupBy</c>/<c>OrderByDescending</c> LINQ expression) for that.
    /// </para>
    /// </summary>
    HideTombstones = 1,
}

/// <summary>
/// Behaviour of the save-changes interceptor when an attempt is made to update
/// an already-tracked versioned entity in place.
/// </summary>
public enum InPlaceUpdateBehavior
{
    /// <summary>
    /// Throw an <see cref="InvalidOperationException"/>. Modifications must be
    /// expressed as new versions via the <c>VersionedDbContext</c> API.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Silently convert the modification into the addition of a new version
    /// row. Useful when migrating an existing codebase to versioning.
    /// </summary>
    ConvertToNewVersion = 1,

    /// <summary>
    /// Allow the in-place update to proceed unchanged. Use only for
    /// administrative scenarios; bypasses the immutability guarantee.
    /// </summary>
    Allow = 2,
}

/// <summary>
/// Granularity to which incoming <c>EffectiveAt</c> timestamps are rounded
/// before being persisted. Rounding is useful when callers cannot guarantee
/// sub-second precision and you want collisions to be detected eagerly.
/// </summary>
public enum EffectiveAtPrecision
{
    /// <summary>Persist the value verbatim.</summary>
    Tick = 0,

    /// <summary>Round down to the nearest microsecond.</summary>
    Microsecond = 1,

    /// <summary>Round down to the nearest millisecond.</summary>
    Millisecond = 2,

    /// <summary>Round down to the nearest second.</summary>
    Second = 3,
}

/// <summary>
/// Configuration options for the bielu content versioning extension. Consumers
/// should retrieve the value through <c>IOptionsMonitor&lt;VersioningOptions&gt;</c>
/// so that runtime changes are honoured.
/// </summary>
public sealed class VersioningOptions
{
    /// <summary>
    /// Default configuration section name used when binding from
    /// <c>IConfiguration</c>.
    /// </summary>
    public const string SectionName = "BieluVersioning";

    /// <summary>
    /// Strategy used to assign new <c>VersionId</c> values. Defaults to
    /// <see cref="VersionIdStrategy.NewGuid"/>.
    /// </summary>
    public VersionIdStrategy VersionIdStrategy { get; set; } = VersionIdStrategy.NewGuid;

    /// <summary>
    /// Default behaviour of the global query filter. Defaults to
    /// <see cref="QueryFilterBehavior.AllVersions"/> — the framework does not
    /// hide history by default; consumers opt in to hiding tombstones via
    /// <see cref="QueryFilterBehavior.HideTombstones"/>.
    /// </summary>
    public QueryFilterBehavior QueryFilterBehavior { get; set; } = QueryFilterBehavior.AllVersions;

    /// <summary>
    /// What to do when EF Core reports an <see cref="InPlaceUpdateBehavior"/>
    /// state change for a versioned entity.
    /// </summary>
    public InPlaceUpdateBehavior InPlaceUpdateBehavior { get; set; } = InPlaceUpdateBehavior.Throw;

    /// <summary>
    /// Granularity to which <c>EffectiveAt</c> values are rounded before being
    /// persisted. Defaults to <see cref="EffectiveAtPrecision.Tick"/> — verbatim.
    /// </summary>
    public EffectiveAtPrecision EffectiveAtPrecision { get; set; } = EffectiveAtPrecision.Tick;

    /// <summary>
    /// When <see langword="true"/>, the save-changes interceptor performs an
    /// explicit existence check for <c>(EntityId, EffectiveAt)</c> collisions
    /// before delegating to the provider, surfacing a clear
    /// <c>EffectiveAtCollisionException</c> instead of an opaque unique-index
    /// violation. Always enabled on non-relational providers; defaults to
    /// <see langword="true"/> on relational ones too for parity.
    /// </summary>
    public bool DetectCollisionsExplicitly { get; set; } = true;

    /// <summary>
    /// When <see langword="true"/>, two distinct version rows for the same
    /// <c>EntityId</c> are allowed to share an identical
    /// <c>EffectiveAt</c> — they are then ordered by <c>VersionId</c>. When
    /// <see langword="false"/> (the default), such writes raise a collision.
    /// </summary>
    public bool AllowEffectiveAtTies { get; set; }
}
