using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Bielu.EntityFramework.Extensions.Versioning;

/// <summary>
/// Convenience extensions for applying versioning conventions to entity types
/// from <c>OnModelCreating</c>.
/// </summary>
public static class VersioningModelBuilderExtensions
{
    /// <summary>
    /// Applies the bielu content-versioning conventions
    /// (<see cref="VersionedEntityConfiguration{TEntity, TEntityId, TVersionId}"/>)
    /// to <typeparamref name="TEntity"/>.
    /// </summary>
    /// <typeparam name="TEntity">The versioned entity type.</typeparam>
    /// <typeparam name="TEntityId">Aggregate identifier type.</typeparam>
    /// <typeparam name="TVersionId">Per-version identifier type.</typeparam>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="options">
    /// The active versioning options. Pass <see langword="null"/> to use defaults.
    /// </param>
    /// <returns>The same <see cref="ModelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyVersioning<TEntity, TEntityId, TVersionId>(
        this ModelBuilder modelBuilder,
        VersioningOptions? options = null)
        where TEntity : class, IVersionedEntity<TEntityId, TVersionId>
        where TEntityId : notnull
        where TVersionId : notnull
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(
            new VersionedEntityConfiguration<TEntity, TEntityId, TVersionId>(options ?? new VersioningOptions()));
        return modelBuilder;
    }
}
