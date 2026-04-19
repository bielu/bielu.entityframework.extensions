using Bielu.EntityFramework.Extensions.Versioning;
using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Example.WebApi;
using Bielu.EntityFramework.Extensions.Versioning.Extensions;
using Bielu.EntityFramework.Extensions.Versioning.Internal;
using Bielu.EntityFramework.Extensions.Versioning.Repository;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register the bielu versioning subsystem (clock + interceptor + options) and
// then the typed repository for our Content aggregate.
builder.Services.AddBieluVersioning();
builder.Services.AddVersionedEntity<ContentDbContext, Content, Guid, Guid>();

// Provider-agnostic configuration: the same code works on InMemory, Sqlite,
// SqlServer, Postgres, MySQL, and Cosmos. Sqlite is used here purely so the
// example runs without any external infrastructure.
builder.Services.AddDbContext<ContentDbContext>((sp, options) =>
{
    options.UseSqlite("DataSource=content.db;Cache=Shared");
    options.UseApplicationServiceProvider(sp);
    options.AddInterceptors(sp.GetRequiredService<VersioningSaveChangesInterceptor>());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
    await ctx.Database.EnsureCreatedAsync();
}

// POST /content/{id}?effectiveAt=...   — create or append (Save / Upsert)
app.MapPost("/content/{id:guid}", async (
    Guid id,
    DateTimeOffset? effectiveAt,
    Content payload,
    IVersionedRepository<Content, Guid, Guid> repo,
    IVersioningClock clock) =>
{
    var result = await repo.SaveAsync(id, effectiveAt ?? clock.UtcNow, payload);
    return Results.Ok(new { result.Kind, result.Entity.VersionId, result.Entity.VersionNumber });
});

// GET /content/{id}?asOf=...           — current version honouring the timeline
app.MapGet("/content/{id:guid}", async (
    Guid id,
    DateTimeOffset? asOf,
    IVersionedRepository<Content, Guid, Guid> repo) =>
{
    var current = await repo.GetCurrentAsync(id, asOf);
    return current is null ? Results.NotFound() : Results.Ok(current);
});

// GET /content/{id}/history            — full ordered timeline
app.MapGet("/content/{id:guid}/history", async (
    Guid id,
    IVersionedRepository<Content, Guid, Guid> repo) =>
        Results.Ok(await repo.GetAllVersionsAsync(id)));

// GET /content/{id}/count              — cheap MAX(VersionNumber) lookup
app.MapGet("/content/{id:guid}/count", async (
    Guid id,
    IVersionedRepository<Content, Guid, Guid> repo) =>
        Results.Ok(new { count = await repo.GetVersionCountAsync(id) }));

await app.RunAsync();
