using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.ChangeTracking;
using Bielu.EntityFramework.Extensions.Versioning.Modeling;
using Bielu.EntityFramework.Extensions.Versioning.Registration;
using Bielu.EntityFramework.Extensions.Versioning.Saving;
using Bielu.EntityFramework.Extensions.Versioning.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.EntityFramework.Extensions.Versioning.Tests;

public class BulkAndUpsertTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddDays(1);
    private static readonly DateTimeOffset T2 = T0.AddDays(2);
    private static readonly DateTimeOffset T3 = T0.AddDays(3);

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task UpsertAsync_creates_aggregate_when_absent(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var entityId = Guid.NewGuid();

        var result = await harness.Context.UpsertAsync<Content, Guid, Guid>(entityId, T1, new Content { Title = "v1" });

        result.Kind.ShouldBe(VersionKind.Initial);
        result.Entity.VersionNumber.ShouldBe(1);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task UpsertAsync_appends_when_aggregate_exists(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var entityId = Guid.NewGuid();

        await harness.Context.SaveAsync<Content, Guid, Guid>(entityId, T1, new Content { Title = "v1" });
        var second = await harness.Context.UpsertAsync<Content, Guid, Guid>(entityId, T2, new Content { Title = "v2" });

        second.Kind.ShouldBe(VersionKind.Current);
        second.Entity.VersionNumber.ShouldBe(2);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task SaveManyAsync_persists_all_in_one_savechanges_call(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var entityId = Guid.NewGuid();

        var requests = new[]
        {
            new VersionWriteRequest<Content, Guid>(entityId, T1, new Content { Title = "v1" }),
            new VersionWriteRequest<Content, Guid>(entityId, T2, new Content { Title = "v2" }),
            new VersionWriteRequest<Content, Guid>(entityId, T3, new Content { Title = "v3" }),
        };

        var results = await harness.Context.SaveManyAsync<Content, Guid, Guid>(requests);

        results.Count.ShouldBe(3);
        results[0].Kind.ShouldBe(VersionKind.Initial);
        results[1].Kind.ShouldBe(VersionKind.Current);
        results[2].Kind.ShouldBe(VersionKind.Current);
        results.Select(r => r.Entity.VersionNumber).ShouldBe(new[] { 1, 2, 3 });
        (await harness.Context.GetVersionCountAsync<Content, Guid, Guid>(entityId)).ShouldBe(3);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task SaveManyAsync_classifies_back_dated_entries_as_archive(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var entityId = Guid.NewGuid();

        var requests = new[]
        {
            new VersionWriteRequest<Content, Guid>(entityId, T3, new Content { Title = "v3" }),
            new VersionWriteRequest<Content, Guid>(entityId, T1, new Content { Title = "v1" }),
            new VersionWriteRequest<Content, Guid>(entityId, T2, new Content { Title = "v2" }),
        };

        var results = await harness.Context.SaveManyAsync<Content, Guid, Guid>(requests);

        results[0].Kind.ShouldBe(VersionKind.Initial);  // T3 is the first one
        results[1].Kind.ShouldBe(VersionKind.Archive);  // T1 < T3
        results[2].Kind.ShouldBe(VersionKind.Archive);  // T2 < T3
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task UpdateManyAsync_throws_when_any_aggregate_is_missing(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var existingEntityId = Guid.NewGuid();
        var missingEntityId = Guid.NewGuid();

        await harness.Context.SaveAsync<Content, Guid, Guid>(existingEntityId, T1, new Content { Title = "v1" });

        var requests = new[]
        {
            new VersionWriteRequest<Content, Guid>(existingEntityId, T2, new Content { Title = "v2" }),
            new VersionWriteRequest<Content, Guid>(missingEntityId, T2, new Content { Title = "ghost" }),
        };

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await harness.Context.UpdateManyAsync<Content, Guid, Guid>(requests));
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Sqlite)]
    public async Task UpsertManyAsync_handles_mix_of_new_and_existing_aggregates(TestProvider provider)
    {
        await using var harness = await TestHarness.CreateAsync(provider);
        var existingEntityId = Guid.NewGuid();
        var brandNewEntityId = Guid.NewGuid();

        await harness.Context.SaveAsync<Content, Guid, Guid>(existingEntityId, T1, new Content { Title = "old-v1" });

        var requests = new[]
        {
            new VersionWriteRequest<Content, Guid>(existingEntityId, T2, new Content { Title = "old-v2" }),
            new VersionWriteRequest<Content, Guid>(brandNewEntityId, T1, new Content { Title = "new-v1" }),
        };

        var results = await harness.Context.UpsertManyAsync<Content, Guid, Guid>(requests);

        results[0].Kind.ShouldBe(VersionKind.Current);   // appended to existing aggregate
        results[1].Kind.ShouldBe(VersionKind.Initial);   // brand-new aggregate
    }

    [Fact]
    public async Task AddVersionedDbContext_wires_application_services_and_interceptor()
    {
        var fakeClock = new FakeVersioningClock(T1);
        var services = new ServiceCollection();
        services.AddSingleton<IVersioningClock>(fakeClock);
        services.AddVersionedDbContext<RegistrationTestContext>((_, options) =>
            options.UseInMemoryDatabase($"add-versioned-{Guid.NewGuid()}"));
        await using var sp = services.BuildServiceProvider();

        await using var scope = sp.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<RegistrationTestContext>();
        await ctx.Database.EnsureCreatedAsync();

        var entityId = Guid.NewGuid();

        var initial = await ctx.SaveAsync<Content, Guid, Guid>(entityId, T1, new Content { Title = "v1" });
        initial.Kind.ShouldBe(VersionKind.Initial);

        var second = await ctx.UpsertAsync<Content, Guid, Guid>(entityId, T2, new Content { Title = "v2" });
        second.Kind.ShouldBe(VersionKind.Current);

        // The interceptor is responsible for stamping RecordedAt from the
        // registered clock — verifying that proves the AddVersionedDbContext
        // wiring took effect end-to-end.
        initial.Entity.RecordedAt.ShouldBe(T1);

        (await ctx.GetVersionCountAsync<Content, Guid, Guid>(entityId)).ShouldBe(2);
    }

    [Fact]
    public async Task VersionedDbContext_base_class_exposes_save_update_helpers()
    {
        var fakeClock = new FakeVersioningClock(T1);
        var services = new ServiceCollection();
        services.AddSingleton<IVersioningClock>(fakeClock);
        services.AddVersionedDbContext<RegistrationTestContext>((_, options) =>
            options.UseInMemoryDatabase($"db-base-{Guid.NewGuid()}"));
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<RegistrationTestContext>();
        await ctx.Database.EnsureCreatedAsync();

        var entityId = Guid.NewGuid();
        var initial = await ctx.SaveAsync<Content, Guid, Guid>(entityId, T1, new Content { Title = "v1" });
        initial.Kind.ShouldBe(VersionKind.Initial);
        var update = await ctx.UpdateAsync<Content, Guid, Guid>(entityId, T2, new Content { Title = "v2" });
        update.Kind.ShouldBe(VersionKind.Current);

        var bulk = await ctx.SaveManyAsync<Content, Guid, Guid>(new[]
        {
            new VersionWriteRequest<Content, Guid>(entityId, T3, new Content { Title = "v3" }),
        });
        bulk[0].Kind.ShouldBe(VersionKind.Current);
        (await ctx.GetVersionCountAsync<Content, Guid, Guid>(entityId)).ShouldBe(3);
    }

    /// <summary>
    /// Test-local <see cref="VersionedDbContext"/> derivative that just hosts
    /// the <see cref="Content"/> set; mirrors what consumers will do.
    /// </summary>
    public sealed class RegistrationTestContext(DbContextOptions<RegistrationTestContext> options)
        : VersionedDbContext(options)
    {
        public DbSet<Content> Contents => Set<Content>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            modelBuilder.ApplyVersioning<Content, Guid, Guid>(new VersioningOptions());
        }
    }
}
