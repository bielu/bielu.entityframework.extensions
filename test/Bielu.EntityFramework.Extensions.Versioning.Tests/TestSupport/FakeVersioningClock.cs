using Bielu.EntityFramework.Extensions.Versioning.Abstractions;

namespace Bielu.EntityFramework.Extensions.Versioning.Tests.TestSupport;

internal sealed class FakeVersioningClock(DateTimeOffset initial) : IVersioningClock
{
    private DateTimeOffset _now = initial;

    public DateTimeOffset UtcNow => _now;

    public void Set(DateTimeOffset value) => _now = value;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
