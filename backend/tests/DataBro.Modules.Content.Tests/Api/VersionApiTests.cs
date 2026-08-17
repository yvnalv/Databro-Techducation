using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataBro.Modules.Identity.Domain;
using Xunit;

namespace DataBro.Modules.Content.Tests.Api;

/// <summary>
/// Version history and restore (CT-8), plus cancelling a schedule (CT-7).
/// </summary>
public class VersionApiTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    private Task<HttpClient> EditorClientAsync() => factory.CreateAuthenticatedClientAsync(Roles.Editor);
    private Task<HttpClient> AuthorClientAsync() => factory.CreateAuthenticatedClientAsync(Roles.Author);

    private static async Task<(HttpStatusCode Status, JsonElement Root)> ReadAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, JsonDocument.Parse(json).RootElement);
    }

    private static object Payload(string slug, string title, string body) => new
    {
        title,
        summary = "A summary",
        slug,
        content = new
        {
            version = 1,
            blocks = new[] { new { id = "b0", type = "paragraph", data = new { text = body } } },
        },
    };

    /// <summary>Creates an article and publishes it twice, leaving two versions behind.</summary>
    private static async Task<Guid> CreateWithTwoVersionsAsync(HttpClient editor)
    {
        var slug = $"versions-{Guid.NewGuid():N}";

        var create = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/articles", Payload(slug, "First Title", "First cut.")));
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();

        await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null);

        await editor.PatchAsJsonAsync($"/api/v1/authoring/articles/{id}", new
        {
            title = "Second Title",
            summary = "A newer summary",
            content = new
            {
                version = 1,
                blocks = new[] { new { id = "b0", type = "paragraph", data = new { text = "Second cut." } } },
            },
        });

        await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null);
        return id;
    }

    [Fact]
    public async Task Lists_versions_newest_first_and_marks_the_current_one()
    {
        var editor = await EditorClientAsync();
        var id = await CreateWithTwoVersionsAsync(editor);

        var response = await ReadAsync(await editor.GetAsync($"/api/v1/authoring/articles/{id}/versions"));
        var versions = response.Root.GetProperty("data").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(2, versions.Length);
        Assert.Equal(2, versions[0].GetProperty("version").GetInt32());
        Assert.True(versions[0].GetProperty("isCurrent").GetBoolean());
        Assert.False(versions[1].GetProperty("isCurrent").GetBoolean());
        Assert.Equal("Second Title", versions[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task A_single_version_carries_its_content()
    {
        var editor = await EditorClientAsync();
        var id = await CreateWithTwoVersionsAsync(editor);

        var response = await ReadAsync(await editor.GetAsync($"/api/v1/authoring/articles/{id}/versions/1"));
        var data = response.Root.GetProperty("data");

        Assert.Equal("First Title", data.GetProperty("title").GetString());
        Assert.Equal(
            "First cut.",
            data.GetProperty("content").GetProperty("blocks")[0].GetProperty("data").GetProperty("text").GetString());
    }

    [Fact]
    public async Task Restoring_loads_the_old_content_into_the_draft()
    {
        var editor = await EditorClientAsync();
        var id = await CreateWithTwoVersionsAsync(editor);

        var restore = await ReadAsync(
            await editor.PostAsync($"/api/v1/authoring/articles/{id}/versions/1/restore", null));

        Assert.Equal(HttpStatusCode.OK, restore.Status);
        Assert.Equal("First Title", restore.Root.GetProperty("data").GetProperty("title").GetString());
    }

    [Fact]
    public async Task Restoring_does_not_change_what_the_public_sees()
    {
        // CT-8 at the HTTP boundary: a restore is a draft edit. The live page keeps serving the
        // published snapshot until someone deliberately publishes again.
        var editor = await EditorClientAsync();
        var id = await CreateWithTwoVersionsAsync(editor);

        var article = await ReadAsync(await editor.GetAsync($"/api/v1/authoring/articles/{id}"));
        var slug = article.Root.GetProperty("data").GetProperty("slug").GetString()!;

        await editor.PostAsync($"/api/v1/authoring/articles/{id}/versions/1/restore", null);

        var published = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/articles/{slug}"));
        Assert.Equal("Second Title", published.Root.GetProperty("data").GetProperty("title").GetString());
    }

    [Fact]
    public async Task Restoring_then_publishing_appends_a_new_version()
    {
        var editor = await EditorClientAsync();
        var id = await CreateWithTwoVersionsAsync(editor);

        await editor.PostAsync($"/api/v1/authoring/articles/{id}/versions/1/restore", null);
        await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null);

        var response = await ReadAsync(await editor.GetAsync($"/api/v1/authoring/articles/{id}/versions"));
        var versions = response.Root.GetProperty("data").EnumerateArray().ToArray();

        // History grew rather than being rewritten — three entries, not two.
        Assert.Equal(3, versions.Length);
        Assert.Equal(3, versions[0].GetProperty("version").GetInt32());
        Assert.Equal("First Title", versions[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task An_author_may_read_and_restore_versions()
    {
        // Restoring is a draft operation, so Content.Edit is enough — an Author does not need the
        // publishing permission to undo their own work in progress (CT-4).
        var editor = await EditorClientAsync();
        var id = await CreateWithTwoVersionsAsync(editor);
        var author = await AuthorClientAsync();

        Assert.Equal(HttpStatusCode.OK, (await author.GetAsync($"/api/v1/authoring/articles/{id}/versions")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await author.PostAsync($"/api/v1/authoring/articles/{id}/versions/1/restore", null)).StatusCode);
    }

    [Fact]
    public async Task Version_history_is_not_public()
    {
        var editor = await EditorClientAsync();
        var id = await CreateWithTwoVersionsAsync(editor);

        var anonymous = factory.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/v1/authoring/articles/{id}/versions")).StatusCode);
    }

    [Fact]
    public async Task Restoring_an_unknown_version_is_a_404()
    {
        var editor = await EditorClientAsync();
        var id = await CreateWithTwoVersionsAsync(editor);

        var response = await editor.PostAsync($"/api/v1/authoring/articles/{id}/versions/99/restore", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Draft/published separation for title and summary (CT-6) ----

    [Fact]
    public async Task An_unpublished_draft_edit_does_not_change_the_public_page()
    {
        // Regression. `title` and `summary` were single columns shared by the draft and the
        // published copy, so typing a new headline changed the live page, the listings, the sitemap,
        // the RSS feed and the search index the moment it was saved — the body was protected and
        // these two never were.
        var editor = await EditorClientAsync();
        var slug = $"draft-leak-{Guid.NewGuid():N}";

        var create = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/articles", Payload(slug, "Published Title", "Published body.")));
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();
        await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null);

        await editor.PatchAsJsonAsync($"/api/v1/authoring/articles/{id}", new
        {
            title = "HALF-WRITTEN DRAFT",
            summary = "not ready",
            content = new
            {
                version = 1,
                blocks = new[] { new { id = "b0", type = "paragraph", data = new { text = "Draft body." } } },
            },
        });

        var anonymous = factory.CreateClient();

        var detail = await ReadAsync(await anonymous.GetAsync($"/api/v1/articles/{slug}"));
        Assert.Equal("Published Title", detail.Root.GetProperty("data").GetProperty("title").GetString());
        Assert.Equal("A summary", detail.Root.GetProperty("data").GetProperty("summary").GetString());

        // The listing is a separate mapping and leaked independently.
        var listing = await ReadAsync(await anonymous.GetAsync("/api/v1/articles?pageSize=100"));
        var listed = listing.Root.GetProperty("data").EnumerateArray()
            .Single(a => a.GetProperty("slug").GetString() == slug);
        Assert.Equal("Published Title", listed.GetProperty("title").GetString());
    }

    [Fact]
    public async Task The_cms_list_still_shows_the_draft_title()
    {
        // The other half of the same rule: an editor must see what they are working on.
        var editor = await EditorClientAsync();
        var slug = $"cms-draft-{Guid.NewGuid():N}";

        var create = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/articles", Payload(slug, "Published Title", "Body.")));
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();
        await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null);

        await editor.PatchAsJsonAsync($"/api/v1/authoring/articles/{id}", new
        {
            title = "Work In Progress",
            summary = "A summary",
            content = new
            {
                version = 1,
                blocks = new[] { new { id = "b0", type = "paragraph", data = new { text = "Body." } } },
            },
        });

        var detail = await ReadAsync(await editor.GetAsync($"/api/v1/authoring/articles/{id}"));
        Assert.Equal("Work In Progress", detail.Root.GetProperty("data").GetProperty("title").GetString());

        var list = await ReadAsync(await editor.GetAsync("/api/v1/authoring/articles?pageSize=100"));
        var listed = list.Root.GetProperty("data").EnumerateArray()
            .Single(a => a.GetProperty("slug").GetString() == slug);
        Assert.Equal("Work In Progress", listed.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Search_indexes_the_published_title_not_the_draft()
    {
        var editor = await EditorClientAsync();
        var slug = $"search-draft-{Guid.NewGuid():N}";
        var draftOnlyWord = $"zarquon{Guid.NewGuid():N}"[..18];

        var create = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/articles", Payload(slug, "Ordinary Published Title", "Body.")));
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();
        await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null);

        await editor.PatchAsJsonAsync($"/api/v1/authoring/articles/{id}", new
        {
            title = draftOnlyWord,
            summary = "A summary",
            content = new
            {
                version = 1,
                blocks = new[] { new { id = "b0", type = "paragraph", data = new { text = "Body." } } },
            },
        });

        var results = await ReadAsync(
            await factory.CreateClient().GetAsync($"/api/v1/search?q={draftOnlyWord}"));

        // Segmented since ADR-0014, so this reads Content's own segment.
        var articles = results.Root.GetProperty("data").GetProperty("segments").EnumerateArray()
            .Single(s => s.GetProperty("kind").GetString() == "articles");

        Assert.Equal(0, articles.GetProperty("total").GetInt32());
    }

    // ---- Cancelling a schedule (CT-7) ----

    [Fact]
    public async Task Cancelling_a_schedule_returns_the_article_to_draft()
    {
        var editor = await EditorClientAsync();
        var slug = $"unschedule-{Guid.NewGuid():N}";

        var create = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/articles", Payload(slug, "Scheduled", "Body.")));
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();

        await editor.PostAsJsonAsync(
            $"/api/v1/authoring/articles/{id}/schedule",
            new { scheduledFor = DateTimeOffset.UtcNow.AddDays(7) });

        var response = await ReadAsync(await editor.PostAsync($"/api/v1/authoring/articles/{id}/unschedule", null));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("draft", response.Root.GetProperty("data").GetProperty("status").GetString());
        Assert.True(
            response.Root.GetProperty("data").TryGetProperty("scheduledFor", out var scheduledFor) is false
            || scheduledFor.ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Cancelling_a_schedule_that_was_never_set_is_a_conflict()
    {
        var editor = await EditorClientAsync();
        var slug = $"unschedule-none-{Guid.NewGuid():N}";

        var create = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/articles", Payload(slug, "Just a draft", "Body.")));
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();

        var response = await editor.PostAsync($"/api/v1/authoring/articles/{id}/unschedule", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task An_author_may_not_cancel_a_schedule()
    {
        // Scheduling is a publishing act (CT-4), and so is undoing one.
        var editor = await EditorClientAsync();
        var slug = $"unschedule-perm-{Guid.NewGuid():N}";

        var create = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/articles", Payload(slug, "Scheduled", "Body.")));
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();

        await editor.PostAsJsonAsync(
            $"/api/v1/authoring/articles/{id}/schedule",
            new { scheduledFor = DateTimeOffset.UtcNow.AddDays(7) });

        var author = await AuthorClientAsync();
        var response = await author.PostAsync($"/api/v1/authoring/articles/{id}/unschedule", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
