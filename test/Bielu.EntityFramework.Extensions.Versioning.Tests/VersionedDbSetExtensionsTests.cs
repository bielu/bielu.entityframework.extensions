using Bielu.EntityFramework.Extensions.Versioning.Reading;
using Bielu.EntityFramework.Extensions.Versioning.Saving;
using Bielu.EntityFramework.Extensions.Versioning.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Bielu.EntityFramework.Extensions.Versioning.Tests;

/// <summary>
/// Smoke tests for the <see cref="DbSet{TEntity}"/> extension surface. The
/// extensions delegate to the same internal save/read engine as
/// <see cref="VersionedDbContext"/>, so we only verify that the wiring
/// correctly resolves the parent context and produces matching results — the
/// engine itself is exhaustively covered elsewhere.
/// </summary>
public class VersionedDbSetExtensionsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddDays(1);
    private static readonly DateTimeOffset T2 = T0.AddDays(2);
    private static readonly DateTimeOffset T3 = T0.AddDays(3);

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task DbSet_SaveAsync_persists_and_classifies_versions(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var entityId = Guid.NewGuid();

        var first = await harness.Context.Contents.SaveAsync<Content, Guid, Guid>(
            entityId, T1, new Content { Title = "v1" });
        var second = await harness.Context.Contents.SaveAsync<Content, Guid, Guid>(
            entityId, T3, new Content { Title = "v3" });
        var middle = await harness.Context.Contents.SaveAsync<Content, Guid, Guid>(
            entityId, T2, new Content { Title = "v2" });

        first.Kind.ShouldBe(VersionKind.Initial);
        second.Kind.ShouldBe(VersionKind.Current);
        middle.Kind.ShouldBe(VersionKind.Archive);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task DbSet_GetCurrentAsync_returns_latest_by_EffectiveAt(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var entityId = Guid.NewGuid();
        harness.Clock.Set(T3.AddHours(1));

        await harness.Context.Contents.SaveAsync<Content, Guid, Guid>(entityId, T1, new Content { Title = "v1" });
        await harness.Context.Contents.SaveAsync<Content, Guid, Guid>(entityId, T3, new Content { Title = "v3" });
        await harness.Context.Contents.SaveAsync<Content, Guid, Guid>(entityId, T2, new Content { Title = "v2" });

        var current = await harness.Context.Contents.GetCurrentAsync<Content, Guid, Guid>(entityId);
        current.ShouldNotBeNull();
        current!.Title.ShouldBe("v3");
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task DbSet_and_DbContext_surfaces_produce_identical_results(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var entityId = Guid.NewGuid();

        // Mix the two surfaces freely: a write through the DbSet must be
        // visible to a read through the DbContext (and vice versa).
        await harness.Context.Contents.SaveAsync<Content, Guid, Guid>(entityId, T1, new Content { Title = "v1" });
        await harness.Context.SaveAsync<Content, Guid, Guid>(entityId, T2, new Content { Title = "v2" });
        await harness.Context.Contents.SaveAsync<Content, Guid, Guid>(entityId, T3, new Content { Title = "v3" });

        var viaSet = await harness.Context.Contents.GetAllVersionsAsync<Content, Guid, Guid>(entityId);
        var viaContext = await harness.Context.GetAllVersionsAsync<Content, Guid, Guid>(entityId);
        viaSet.Select(v => v.Title).ShouldBe(viaContext.Select(v => v.Title));

        (await harness.Context.Contents.GetVersionCountAsync<Content, Guid, Guid>(entityId))
            .ShouldBe(await harness.Context.GetVersionCountAsync<Content, Guid, Guid>(entityId));
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task DbSet_UpdateAsync_throws_when_aggregate_missing(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var entityId = Guid.NewGuid();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await harness.Context.Contents.UpdateAsync<Content, Guid, Guid>(
                entityId, T1, new Content { Title = "v1" }));
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task DbSet_SaveManyAsync_persists_in_one_savechanges_call(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var entityId = Guid.NewGuid();

        var requests = new[]
        {
            new VersionWriteRequest<Content, Guid>(entityId, T1, new Content { Title = "v1" }),
            new VersionWriteRequest<Content, Guid>(entityId, T2, new Content { Title = "v2" }),
            new VersionWriteRequest<Content, Guid>(entityId, T3, new Content { Title = "v3" }),
        };

        var results = await harness.Context.Contents.SaveManyAsync<Content, Guid, Guid>(requests);

        results.Count.ShouldBe(3);
        (await harness.Context.Contents.GetVersionCountAsync<Content, Guid, Guid>(entityId)).ShouldBe(3);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task DbSet_SoftDeleteVersionAsync_writes_tombstone(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var entityId = Guid.NewGuid();
        harness.Clock.Set(T3);

        await harness.Context.Contents.SaveAsync<Content, Guid, Guid>(entityId, T1, new Content { Title = "v1" });
        await harness.Context.Contents.SoftDeleteVersionAsync<Content, Guid, Guid>(
            entityId, T2, new Content { Title = "tombstone" });

        (await harness.Context.Contents.GetCurrentAsync<Content, Guid, Guid>(entityId)).ShouldBeNull();
        (await harness.Context.Contents.GetAllVersionsAsync<Content, Guid, Guid>(entityId)).Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task DbSet_GetNeighborsAsync_returns_predecessor_and_successor(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var entityId = Guid.NewGuid();

        await harness.Context.Contents.SaveAsync<Content, Guid, Guid>(entityId, T1, new Content { Title = "v1" });
        await harness.Context.Contents.SaveAsync<Content, Guid, Guid>(entityId, T3, new Content { Title = "v3" });

        var n = await harness.Context.Contents.GetNeighborsAsync<Content, Guid, Guid>(entityId, T2);
        n.Previous!.Title.ShouldBe("v1");
        n.Next!.Title.ShouldBe("v3");
    }
}
