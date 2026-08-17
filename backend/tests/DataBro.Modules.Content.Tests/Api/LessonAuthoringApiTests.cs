using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataBro.Modules.Identity.Domain;
using Xunit;

namespace DataBro.Modules.Content.Tests.Api;

/// <summary>
/// Authoring lesson bodies (ADR-0012). The half of the loop that lets a course be built without
/// inserting rows by hand.
/// </summary>
public class LessonAuthoringApiTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    private Task<HttpClient> EditorAsync() => factory.CreateAuthenticatedClientAsync(Roles.Editor);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static object Payload(string title, string body, string? slug = null) => new
    {
        title,
        summary = "A lesson summary",
        slug = slug ?? $"lesson-{Guid.NewGuid():N}",
        content = new
        {
            version = 1,
            blocks = new[] { new { id = "b0", type = "paragraph", data = new { text = body } } },
        },
    };

    private async Task<(HttpClient Editor, Guid Id)> CreateAsync(string title = "Chunking", string body = "Body.")
    {
        var editor = await EditorAsync();
        var created = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/lessons", Payload(title, body)));

        return (editor, created.GetProperty("data").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_lesson_body_can_be_created_and_published()
    {
        var (editor, id) = await CreateAsync("Chunking Strategies");

        var draft = await ReadAsync(await editor.GetAsync($"/api/v1/authoring/lessons/{id}"));
        Assert.Equal("draft", draft.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(0, draft.GetProperty("data").GetProperty("currentVersion").GetInt32());

        var published = await ReadAsync(await editor.PostAsync($"/api/v1/authoring/lessons/{id}/publish", null));
        Assert.Equal("published", published.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(1, published.GetProperty("data").GetProperty("currentVersion").GetInt32());
    }

    [Fact]
    public async Task It_gets_the_engine_for_free_including_version_history()
    {
        // The payoff of ADR-0007: no versioning code was written for lessons.
        var (editor, id) = await CreateAsync("First", "First cut.");
        await editor.PostAsync($"/api/v1/authoring/lessons/{id}/publish", null);

        await editor.PatchAsJsonAsync($"/api/v1/authoring/lessons/{id}", new
        {
            title = "Second",
            summary = "A newer summary",
            content = new
            {
                version = 1,
                blocks = new[] { new { id = "b0", type = "paragraph", data = new { text = "Second cut." } } },
            },
        });
        await editor.PostAsync($"/api/v1/authoring/lessons/{id}/publish", null);

        var versions = await ReadAsync(await editor.GetAsync($"/api/v1/authoring/lessons/{id}/versions"));
        var entries = versions.GetProperty("data").EnumerateArray().ToArray();

        Assert.Equal(2, entries.Length);
        Assert.True(entries[0].GetProperty("isCurrent").GetBoolean());

        var restored = await ReadAsync(
            await editor.PostAsync($"/api/v1/authoring/lessons/{id}/versions/1/restore", null));
        Assert.Equal("First", restored.GetProperty("data").GetProperty("title").GetString());
    }

    [Fact]
    public async Task A_lesson_body_cannot_take_a_slug_an_article_holds()
    {
        // Uniqueness spans both tables, checked on the write path because an index cannot span them.
        var editor = await EditorAsync();
        var slug = $"shared-{Guid.NewGuid():N}";

        var article = await editor.PostAsJsonAsync("/api/v1/authoring/articles", new
        {
            title = "An Article",
            summary = "A summary",
            slug,
            content = new
            {
                version = 1,
                blocks = new[] { new { id = "b0", type = "paragraph", data = new { text = "Body." } } },
            },
        });
        article.EnsureSuccessStatusCode();

        var lesson = await editor.PostAsJsonAsync(
            "/api/v1/authoring/lessons", Payload("Colliding Lesson", "Body.", slug));

        Assert.Equal(HttpStatusCode.Conflict, lesson.StatusCode);
    }

    [Fact]
    public async Task Lesson_bodies_are_listed_for_the_picker()
    {
        var (editor, id) = await CreateAsync("Findable Lesson");

        var list = await ReadAsync(await editor.GetAsync("/api/v1/authoring/lessons?pageSize=100"));
        var ids = list.GetProperty("data").EnumerateArray().Select(l => l.GetProperty("id").GetGuid());

        Assert.Contains(id, ids);
    }

    [Fact]
    public async Task There_is_no_public_endpoint_for_a_lesson_body()
    {
        // A lesson is reached through its course. A public route here would be a second URL for the
        // same content, outside any curriculum.
        var (editor, id) = await CreateAsync("Not Public");
        await editor.PostAsync($"/api/v1/authoring/lessons/{id}/publish", null);

        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/lessons/{id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/v1/authoring/lessons/{id}")).StatusCode);
    }

    [Fact]
    public async Task An_author_may_write_a_lesson_but_not_publish_it()
    {
        var author = await factory.CreateAuthenticatedClientAsync(Roles.Author);

        var created = await author.PostAsJsonAsync("/api/v1/authoring/lessons", Payload("Authored", "Body."));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var id = (await ReadAsync(created)).GetProperty("data").GetProperty("id").GetGuid();
        var publish = await author.PostAsync($"/api/v1/authoring/lessons/{id}/publish", null);

        Assert.Equal(HttpStatusCode.Forbidden, publish.StatusCode);
    }

    [Fact]
    public async Task An_empty_lesson_cannot_be_published()
    {
        var editor = await EditorAsync();
        var created = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/lessons", new
        {
            title = "Titled But Empty",
            summary = "A summary",
            slug = $"empty-{Guid.NewGuid():N}",
            content = new { version = 1, blocks = Array.Empty<object>() },
        }));

        var response = await editor.PostAsync(
            $"/api/v1/authoring/lessons/{created.GetProperty("data").GetProperty("id").GetGuid()}/publish", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
