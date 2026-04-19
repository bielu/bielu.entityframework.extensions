namespace Bielu.EntityFramework.Extensions.Versioning.Reading;

/// <summary>
/// A predecessor/successor pair of versions surrounding a given
/// <c>EffectiveAt</c> point on a versioned aggregate's timeline.
/// </summary>
/// <typeparam name="TEntity">The versioned entity type.</typeparam>
public sealed record VersionNeighbors<TEntity>
    where TEntity : class
{
    /// <summary>
    /// The version that immediately precedes the queried <c>EffectiveAt</c>
    /// (i.e. the version with the greatest <c>EffectiveAt</c> &lt;= the query
    /// point). <see langword="null"/> when no such version exists.
    /// </summary>
    public TEntity? Previous { get; init; }

    /// <summary>
    /// The version that immediately follows the queried <c>EffectiveAt</c>
    /// (i.e. the version with the smallest <c>EffectiveAt</c> &gt; the query
    /// point). <see langword="null"/> when no such version exists.
    /// </summary>
    public TEntity? Next { get; init; }
}
