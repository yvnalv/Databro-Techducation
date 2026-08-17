using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataBro.Modules.Content.Domain;
using DataBro.Modules.Content.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Tests.Api;

/// <summary>
/// The reason ADR-0012 chose separate tables over a <c>kind</c> discriminator: a published lesson
/// body must not be reachable through any surface meant for articles.
///
/// <para>
/// With a discriminator these would be tests of a predicate that someone must remember to write on
/// every read path. Here they assert a structural property — a lesson is not in the articles table —
/// so they pass by construction and would only fail if the design were undone.
/// </para>
/// </summary>
public class LessonIsolationTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    /// <summary>Creates and publishes a lesson body directly: there is no authoring API for one yet.</summary>
    private async Task<(Guid Id, string Slug)> PublishLessonAsync(string title)
    {
        var slug = $"lesson-{Guid.NewGuid():N}";

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        var lesson = LessonContent.CreateDraft(
            Guid.NewGuid(),
            Slug.Create(slug),
            title,
            "A lesson summary",
            new ContentDocument
            {
                Version = 1,
                Blocks =
                [
                    new ContentBlock
                    {
                        Id = "b0",
                        Type = "paragraph",
                        Data = new System.Text.Json.Nodes.JsonObject { ["text"] = "Lesson body text." },
                    },
                ],
            });

        lesson.Publish(DateTimeOffset.UtcNow);

        db.LessonContents.Add(lesson);
        await db.SaveChangesAsync();

        return (lesson.Id, slug);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task A_published_lesson_is_not_served_as_an_article()
    {
        var (_, slug) = await PublishLessonAsync("A Lesson, Not An Article");

        var response = await factory.CreateClient().GetAsync($"/api/v1/articles/{slug}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_published_lesson_does_not_appear_in_the_article_listing()
    {
        // The listing feeds the homepage, the sitemap and the RSS feed, so this one assertion covers
        // the surfaces a discriminator would have had to be remembered on individually.
        var (id, _) = await PublishLessonAsync("Listing Isolation");

        var listing = await ReadAsync(await factory.CreateClient().GetAsync("/api/v1/articles?pageSize=100"));
        var ids = listing.GetProperty("data").EnumerateArray()
            .Select(a => a.GetProperty("id").GetGuid())
            .ToArray();

        Assert.DoesNotContain(id, ids);
    }

    [Fact]
    public async Task A_published_lesson_is_not_returned_by_article_search()
    {
        var token = $"lessonium{Guid.NewGuid():N}"[..20];
        await PublishLessonAsync($"Searchable {token}");

        var results = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/search?q={token}"));

        // Segmented since ADR-0014. Every segment must be empty, not just the articles one: a lesson
        // body has no public URL, so a hit anywhere would point at a page that does not exist.
        foreach (var segment in results.GetProperty("data").GetProperty("segments").EnumerateArray())
            Assert.Equal(0, segment.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task An_article_cannot_take_a_slug_a_lesson_already_holds()
    {
        // The one cost of separate tables: a unique index cannot span them, so uniqueness is a guard
        // on the write path instead (IContentSlugRegistry). Both are URLs on one origin, so without
        // this they would shadow each other.
        var (_, slug) = await PublishLessonAsync("Slug Holder");

        var editor = await factory.CreateAuthenticatedClientAsync(Modules.Identity.Domain.Roles.Editor);
        var response = await editor.PostAsJsonAsync("/api/v1/authoring/articles", new
        {
            title = "Colliding Article",
            summary = "A summary",
            slug,
            content = new
            {
                version = 1,
                blocks = new[] { new { id = "b0", type = "paragraph", data = new { text = "Body." } } },
            },
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("slug_taken", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Lesson_and_article_rows_live_in_different_tables()
    {
        // The structural claim the tests above rest on, asserted directly so a future change to the
        // mapping strategy fails here with an obvious message rather than as three puzzling 200s.
        var (id, _) = await PublishLessonAsync("Table Separation");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        Assert.False(await db.Articles.AnyAsync(a => a.Id == id));
        Assert.True(await db.LessonContents.AnyAsync(l => l.Id == id));
    }
}
