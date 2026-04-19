namespace Bielu.EntityFramework.Extensions.Versioning.Abstractions;

/// <summary>
/// Abstraction over the wall clock used by the versioning subsystem. Always
/// returns UTC values. The default implementation simply forwards to
/// <see cref="DateTimeOffset.UtcNow"/>; tests should substitute an
/// implementation that returns deterministic values.
/// </summary>
public interface IVersioningClock
{
    /// <summary>
    /// Gets the current UTC instant.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// Default <see cref="IVersioningClock"/> implementation that delegates to
/// <see cref="DateTimeOffset.UtcNow"/>.
/// </summary>
public sealed class SystemVersioningClock : IVersioningClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
