namespace Bielu.EntityFramework.Extensions.Versioning.Abstractions;

/// <summary>
/// Base type for all exceptions raised by the bielu content-versioning
/// extension.
/// </summary>
public class VersioningException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="VersioningException"/> class.</summary>
    public VersioningException() { }

    /// <summary>Initialises a new instance of the <see cref="VersioningException"/> class.</summary>
    /// <param name="message">The exception message.</param>
    public VersioningException(string message) : base(message) { }

    /// <summary>Initialises a new instance of the <see cref="VersioningException"/> class.</summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public VersioningException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when the optimistic concurrency token of a versioned entity does
/// not match the value currently stored in the database.
/// </summary>
public sealed class VersionConflictException : VersioningException
{
    /// <summary>Initialises a new instance.</summary>
    public VersionConflictException() : base("A version conflict was detected.") { }

    /// <summary>Initialises a new instance.</summary>
    /// <param name="message">The exception message.</param>
    public VersionConflictException(string message) : base(message) { }

    /// <summary>Initialises a new instance.</summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public VersionConflictException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a new version is added with an <c>EffectiveAt</c> that already
/// exists for the same <c>EntityId</c> and the configured
/// <c>VersioningOptions.AllowEffectiveAtTies</c> policy disallows ties.
/// </summary>
public sealed class EffectiveAtCollisionException : VersioningException
{
    /// <summary>Initialises a new instance.</summary>
    public EffectiveAtCollisionException()
        : base("A version with the same EffectiveAt already exists for this EntityId.") { }

    /// <summary>Initialises a new instance.</summary>
    /// <param name="message">The exception message.</param>
    public EffectiveAtCollisionException(string message) : base(message) { }

    /// <summary>Initialises a new instance.</summary>
    /// <param name="entityId">The aggregate identifier.</param>
    /// <param name="effectiveAt">The colliding effective-at value.</param>
    public EffectiveAtCollisionException(object entityId, DateTimeOffset effectiveAt)
        : base($"A version with EffectiveAt='{effectiveAt:O}' already exists for EntityId='{entityId}'.")
    {
        EntityId = entityId;
        EffectiveAt = effectiveAt;
    }

    /// <summary>Initialises a new instance.</summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public EffectiveAtCollisionException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>The aggregate identifier that triggered the collision, when known.</summary>
    public object? EntityId { get; }

    /// <summary>The colliding effective-at value, when known.</summary>
    public DateTimeOffset? EffectiveAt { get; }
}
