using Bielu.EntityFramework.Extensions.Versioning.Abstractions;
using Bielu.EntityFramework.Extensions.Versioning.Example.WebApi;
using Bielu.EntityFramework.Extensions.Versioning.Registration;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register the bielu versioning subsystem AND the versioned DbContext (with
// UseApplicationServiceProvider + the VersioningSaveChangesInterceptor wired
// for us) in a single call. Provider-agnostic: the same code works on
// InMemory, Sqlite, SqlServer, Postgres, MySQL, and Cosmos. Sqlite is used
// here purely so the example runs without any external infrastructure.
builder.Services.AddVersionedDbContext<ContentDbContext>((_, options) =>
    options.UseSqlite("DataSource=content.db;Cache=Shared"));

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
    ContentDbContext db,
    IVersioningClock clock) =>
{
    var result = await db.SaveAsync<Content, Guid, Guid>(id, effectiveAt ?? clock.UtcNow, payload);
    return Results.Ok(new { result.Kind, result.Entity.VersionId, result.Entity.VersionNumber });
});

// GET /content/{id}?asOf=...           — current version honouring the timeline
app.MapGet("/content/{id:guid}", async (
    Guid id,
    DateTimeOffset? asOf,
    ContentDbContext db) =>
{
    var current = await db.GetCurrentAsync<Content, Guid, Guid>(id, asOf);
    return current is null ? Results.NotFound() : Results.Ok(current);
});

// GET /content/{id}/history            — full ordered timeline
app.MapGet("/content/{id:guid}/history", async (
    Guid id,
    ContentDbContext db) =>
        Results.Ok(await db.GetAllVersionsAsync<Content, Guid, Guid>(id)));

// GET /content/{id}/count              — cheap MAX(VersionNumber) lookup
app.MapGet("/content/{id:guid}/count", async (
    Guid id,
    ContentDbContext db) =>
        Results.Ok(new { count = await db.GetVersionCountAsync<Content, Guid, Guid>(id) }));

await app.RunAsync();
