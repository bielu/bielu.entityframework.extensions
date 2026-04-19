using Bielu.EntityFramework.Extensions.Versioning.Abstractions;

namespace Bielu.EntityFramework.Extensions.Versioning.Saving;

/// <summary>
/// Classifies a version row at the moment it is persisted.
/// </summary>
public enum VersionKind
{
    /// <summary>
    /// The very first version recorded for this <c>EntityId</c>.
    /// </summary>
    Initial = 0,

    /// <summary>
    /// The newly persisted row is the latest version of the aggregate — its
    /// <see cref="IVersionedEntity.EffectiveAt"/> is greater than or equal to
    /// every previously recorded version's <see cref="IVersionedEntity.EffectiveAt"/>
    /// for the same <c>EntityId</c>.
    /// </summary>
    Current = 1,

    /// <summary>
    /// A back-dated / late-arriving version. Its
    /// <see cref="IVersionedEntity.EffectiveAt"/> is strictly less than the
    /// most recent existing version's, so it sits in the past portion of the
    /// timeline (possibly between two existing versions).
    /// </summary>
    Archive = 2,
}
