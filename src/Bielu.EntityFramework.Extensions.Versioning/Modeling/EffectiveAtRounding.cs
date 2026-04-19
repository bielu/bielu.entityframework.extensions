using Bielu.EntityFramework.Extensions.Versioning.Abstractions;

namespace Bielu.EntityFramework.Extensions.Versioning.Modeling;

/// <summary>
/// Helpers that round <see cref="DateTimeOffset"/> values to a given
/// <see cref="EffectiveAtPrecision"/>. Centralising the logic ensures
/// consistent behaviour between the save engine, the interceptor and any
/// consumer-side code that needs to compare timestamps with stored values.
/// </summary>
internal static class EffectiveAtRounding
{
    public static DateTimeOffset Round(DateTimeOffset value, EffectiveAtPrecision precision)
    {
        return precision switch
        {
            EffectiveAtPrecision.Tick => value,
            EffectiveAtPrecision.Microsecond => Truncate(value, TimeSpan.TicksPerMicrosecond),
            EffectiveAtPrecision.Millisecond => Truncate(value, TimeSpan.TicksPerMillisecond),
            EffectiveAtPrecision.Second => Truncate(value, TimeSpan.TicksPerSecond),
            _ => value,
        };
    }

    private static DateTimeOffset Truncate(DateTimeOffset value, long ticksPerUnit)
    {
        var truncated = value.Ticks - (value.Ticks % ticksPerUnit);
        return new DateTimeOffset(truncated, value.Offset);
    }
}
