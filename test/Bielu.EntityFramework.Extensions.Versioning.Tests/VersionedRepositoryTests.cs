using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Repository;
using Bielu.EntityFramework.Extensions.Versioning.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Bielu.EntityFramework.Extensions.Versioning.Tests;

public class VersionedRepositoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddDays(1);
    private static readonly DateTimeOffset T2 = T0.AddDays(2);
    private static readonly DateTimeOffset T3 = T0.AddDays(3);
    private static readonly DateTimeOffset T1_5 = T0.AddDays(1).AddHours(12);
    private static readonly DateTimeOffset T2_5 = T0.AddDays(2).AddHours(12);

    private static VersionedRepository<TestDbContext, Content, Guid, Guid> CreateRepo(TestHarness harness)
        => new(harness.Context, harness.Clock, harness.OptionsMonitor);

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task Save_first_version_is_classified_as_Initial(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();

        var result = await repo.SaveAsync(entityId, T1, new Content { Title = "v1", Body = "body 1" });

        result.Kind.ShouldBe(VersionKind.Initial);
        result.Entity.EntityId.ShouldBe(entityId);
        result.Entity.VersionId.ShouldNotBe(Guid.Empty);
        result.Entity.VersionNumber.ShouldBe(1);
        result.Entity.RecordedAt.ShouldBe(harness.Clock.UtcNow);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task Save_second_version_with_later_EffectiveAt_is_Current(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();

        await repo.SaveAsync(entityId, T1, new Content { Title = "v1" });
        var second = await repo.SaveAsync(entityId, T2, new Content { Title = "v2" });

        second.Kind.ShouldBe(VersionKind.Current);
        second.Entity.VersionNumber.ShouldBe(2);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task Save_back_dated_version_is_classified_as_Archive(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();

        await repo.SaveAsync(entityId, T1, new Content { Title = "v1" });
        await repo.SaveAsync(entityId, T3, new Content { Title = "v3" });

        // Insert v2 in between the two existing versions.
        var middle = await repo.SaveAsync(entityId, T2, new Content { Title = "v2" });

        middle.Kind.ShouldBe(VersionKind.Archive);
        middle.Entity.VersionNumber.ShouldBe(3); // 3rd insertion
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task GetCurrentAsync_after_inserting_in_between_returns_latest_by_EffectiveAt(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();
        harness.Clock.Set(T3.AddHours(1));

        await repo.SaveAsync(entityId, T1, new Content { Title = "v1" });
        await repo.SaveAsync(entityId, T3, new Content { Title = "v3" });
        await repo.SaveAsync(entityId, T2, new Content { Title = "v2" });

        var current = await repo.GetCurrentAsync(entityId);
        current.ShouldNotBeNull();
        current!.Title.ShouldBe("v3");
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task GetCurrentAsync_with_asOf_returns_version_active_at_that_time(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();

        await repo.SaveAsync(entityId, T1, new Content { Title = "v1" });
        await repo.SaveAsync(entityId, T2, new Content { Title = "v2" });
        await repo.SaveAsync(entityId, T3, new Content { Title = "v3" });

        (await repo.GetCurrentAsync(entityId, T1_5))!.Title.ShouldBe("v1");
        (await repo.GetCurrentAsync(entityId, T2_5))!.Title.ShouldBe("v2");
        (await repo.GetCurrentAsync(entityId, T3.AddDays(1)))!.Title.ShouldBe("v3");
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task Out_of_order_arrival_preserves_current_version(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();
        harness.Clock.Set(T3.AddDays(1));

        // Insert v3 first, then v2 (out of order).
        await repo.SaveAsync(entityId, T3, new Content { Title = "v3" });
        var second = await repo.SaveAsync(entityId, T2, new Content { Title = "v2" });

        second.Kind.ShouldBe(VersionKind.Archive);
        var current = await repo.GetCurrentAsync(entityId);
        current!.Title.ShouldBe("v3");
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task GetAllVersionsAsync_returns_chronological_order(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();

        await repo.SaveAsync(entityId, T3, new Content { Title = "v3" });
        await repo.SaveAsync(entityId, T1, new Content { Title = "v1" });
        await repo.SaveAsync(entityId, T2, new Content { Title = "v2" });

        var all = await repo.GetAllVersionsAsync(entityId);
        all.Select(v => v.Title).ShouldBe(new[] { "v1", "v2", "v3" });
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task GetVersionCountAsync_is_cheap_and_correct(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();

        (await repo.GetVersionCountAsync(entityId)).ShouldBe(0);

        await repo.SaveAsync(entityId, T1, new Content { Title = "v1" });
        await repo.SaveAsync(entityId, T3, new Content { Title = "v3" });
        await repo.SaveAsync(entityId, T2, new Content { Title = "v2" });

        (await repo.GetVersionCountAsync(entityId)).ShouldBe(3);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task GetNeighborsAsync_returns_predecessor_and_successor(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();

        await repo.SaveAsync(entityId, T1, new Content { Title = "v1" });
        await repo.SaveAsync(entityId, T3, new Content { Title = "v3" });

        var n = await repo.GetNeighborsAsync(entityId, T2);
        n.Previous.ShouldNotBeNull();
        n.Previous!.Title.ShouldBe("v1");
        n.Next.ShouldNotBeNull();
        n.Next!.Title.ShouldBe("v3");
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task UpdateAsync_throws_when_no_existing_versions(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await repo.UpdateAsync(entityId, T1, new Content { Title = "v1" }));
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task UpdateAsync_works_when_aggregate_already_exists(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();

        await repo.SaveAsync(entityId, T1, new Content { Title = "v1" });
        var update = await repo.UpdateAsync(entityId, T2, new Content { Title = "v2" });

        update.Kind.ShouldBe(VersionKind.Current);
        update.Entity.VersionNumber.ShouldBe(2);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task EffectiveAt_collision_is_detected(TestProvider provider)
    {
        var options = new VersioningOptions { AllowEffectiveAtTies = false, DetectCollisionsExplicitly = true };
        await using var harness = await TestHarness.CreateAsync(provider, options);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();

        await repo.SaveAsync(entityId, T1, new Content { Title = "v1" });
        await Should.ThrowAsync<EffectiveAtCollisionException>(async () =>
            await repo.SaveAsync(entityId, T1, new Content { Title = "v1-collision" }));
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task SoftDelete_marks_subsequent_GetCurrent_as_null(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();
        harness.Clock.Set(T3.AddDays(1));

        await repo.SaveAsync(entityId, T1, new Content { Title = "v1" });
        await repo.SoftDeleteVersionAsync(entityId, T2, new Content { Title = "tombstone" });

        var current = await repo.GetCurrentAsync(entityId);
        current.ShouldBeNull();

        // Historical reads still work (the tombstone itself is a version).
        var history = await repo.GetAllVersionsAsync(entityId);
        history.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task RecordedAt_uses_clock(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();
        var fixedNow = T2;
        harness.Clock.Set(fixedNow);

        var first = await repo.SaveAsync(entityId, T1, new Content { Title = "v1" });
        first.Entity.RecordedAt.ShouldBe(fixedNow);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task InPlace_modification_throws_by_default(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();

        var first = (await repo.SaveAsync(entityId, T1, new Content { Title = "v1" })).Entity;

        // Mutate directly via change tracker — should be rejected on save.
        first.Title = "MUTATED";
        harness.Context.Update(first);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await harness.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Clock_abstraction_is_consulted_per_call()
    {
        await using var harness = await TestHarness.CreateAsync(TestProvider.InMemory);
        var repo = CreateRepo(harness);
        var entityId = Guid.NewGuid();

        harness.Clock.Set(T1);
        var first = (await repo.SaveAsync(entityId, T1, new Content { Title = "v1" })).Entity;
        harness.Clock.Set(T2);
        var second = (await repo.SaveAsync(entityId, T2, new Content { Title = "v2" })).Entity;

        first.RecordedAt.ShouldBe(T1);
        second.RecordedAt.ShouldBe(T2);
    }
}
