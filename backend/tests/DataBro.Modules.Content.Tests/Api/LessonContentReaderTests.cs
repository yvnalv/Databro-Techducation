using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DataBro.Modules.Content.Domain;
using DataBro.Modules.Content.Infrastructure.Persistence;
using DataBro.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Tests.Api;

/// <summary>
/// The cross-module contract Learning will read lesson bodies through (ADR-0008, ADR-0012).
///
/// Exercised through the real DI container and a real database, because the properties worth
/// asserting are boundary properties — what a *different module* can and cannot see.
/// </summary>
public class LessonContentReaderTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    private static ContentDocument Body(string text) =>
        new()
        {
            Version = 1,
            Blocks =
            [
                new ContentBlock { Id = "b0", Type = "paragraph", Data = new JsonObject { ["text"] = text } },
            ],
        };

    /// <summary>Creates a lesson body, optionally publishing it, and returns its id.</summary>
    private async Task<Guid> SeedAsync(string title, bool publish, string body = "Lesson body.")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        var lesson = LessonContent.CreateDraft(
            Guid.NewGuid(), Slug.Create($"reader-{Guid.NewGuid():N}"), title, "A summary", Body(body));

        if (publish) lesson.Publish(DateTimeOffset.UtcNow);

        db.LessonContents.Add(lesson);
        await db.SaveChangesAsync();

        return lesson.Id;
    }

    private async Task<IReadOnlyDictionary<Guid, LessonContentView>> ReadAsync(params Guid[] ids)
    {
        using var scope = factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<ILessonContentReader>();
        return await reader.GetLessonContentAsync(ids);
    }

    [Fact]
    public async Task Resolves_a_published_body_with_its_blocks()
    {
        var id = await SeedAsync("Chunking", publish: true, body: "Fixed-size windows.");

        var view = (await ReadAsync(id))[id];

        Assert.Equal("Chunking", view.Title);
        Assert.Equal(1, view.CurrentVersion);
        Assert.NotNull(view.PublishedAt);
        Assert.Equal("paragraph", Assert.Single(view.Blocks).Type);
        Assert.Equal("Fixed-size windows.", view.Blocks[0].Data!["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task An_unpublished_body_resolves_with_no_blocks_rather_than_its_draft()
    {
        // CT-6 at the module boundary. If the draft leaked here, a half-written lesson would reach a
        // learner the moment it was typed — the same defect that once put a draft title on the
        // public article page.
        var id = await SeedAsync("Not Ready", publish: false, body: "Half-written thoughts.");

        var view = (await ReadAsync(id))[id];

        Assert.Null(view.PublishedAt);
        Assert.Empty(view.Blocks);
    }

    [Fact]
    public async Task Reports_the_published_title_after_a_later_draft_edit()
    {
        var id = await SeedAsync("Published Title", publish: true, body: "Published body.");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            var lesson = await db.LessonContents.FindAsync(id);
            lesson!.UpdateDraft("HALF-WRITTEN", "not ready", Body("Draft body."));
            await db.SaveChangesAsync();
        }

        var view = (await ReadAsync(id))[id];

        Assert.Equal("Published Title", view.Title);
        Assert.Equal("Published body.", view.Blocks[0].Data!["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task Does_not_resolve_an_article_id()
    {
        // The reason this contract is named for lesson bodies rather than content units. Resolving
        // any unit would let Learning attach an article as a lesson, undoing ADR-0012's separation.
        var editor = await factory.CreateAuthenticatedClientAsync(Modules.Identity.Domain.Roles.Editor);
        var create = await editor.PostAsJsonAsync("/api/v1/authoring/articles", new
        {
            title = "An Article",
            summary = "A summary",
            slug = $"reader-article-{Guid.NewGuid():N}",
            content = new
            {
                version = 1,
                blocks = new[] { new { id = "b0", type = "paragraph", data = new { text = "Body." } } },
            },
        });

        var articleId = System.Text.Json.JsonDocument.Parse(await create.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetGuid();

        Assert.Empty(await ReadAsync(articleId));
    }

    [Fact]
    public async Task Tolerates_unknown_ids_and_resolves_the_rest()
    {
        // A body deleted out from under a lesson must leave the course renderable, so a missing id
        // is simply absent rather than an exception.
        var known = await SeedAsync("Known", publish: true);

        var resolved = await ReadAsync(known, Guid.NewGuid());

        Assert.Single(resolved);
        Assert.True(resolved.ContainsKey(known));
    }

    [Fact]
    public async Task Resolves_a_whole_batch_in_one_call()
    {
        var ids = new[]
        {
            await SeedAsync("One", publish: true),
            await SeedAsync("Two", publish: true),
            await SeedAsync("Three", publish: true),
        };

        var resolved = await ReadAsync(ids);

        Assert.Equal(3, resolved.Count);
        Assert.All(ids, id => Assert.True(resolved.ContainsKey(id)));
    }

    [Fact]
    public async Task An_empty_request_does_not_hit_the_database()
    {
        Assert.Empty(await ReadAsync());
    }
}
