using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataBro.Modules.Identity.Domain;
using Xunit;

namespace DataBro.Modules.Content.Tests.Api;

public class ContentApiTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    private Task<HttpClient> EditorClientAsync() => factory.CreateAuthenticatedClientAsync(Roles.Editor);
    private HttpClient AnonymousClient() => factory.CreateClient();

    private static object DraftPayload(string slug, string title = "A Title", params string[] blockTexts)
    {
        var blocks = (blockTexts.Length == 0 ? ["Some body text."] : blockTexts)
            .Select((t, i) => new { id = $"b{i}", type = "paragraph", data = new { text = t } })
            .ToArray();

        return new { title, summary = "A summary", slug, content = new { version = 1, blocks } };
    }

    private static async Task<(HttpStatusCode Status, JsonElement Root)> ReadAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, JsonDocument.Parse(json).RootElement);
    }

    [Fact]
    public async Task Create_publish_and_read_public_happy_path()
    {
        var editor = await EditorClientAsync();
        var anon = AnonymousClient();
        var slug = $"happy-path-{Guid.NewGuid():N}";

        var create = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload(slug)));
        Assert.Equal(HttpStatusCode.OK, create.Status);
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();

        // public read before publish -> 404
        var beforePublish = await ReadAsync(await anon.GetAsync($"/api/v1/articles/{slug}"));
        Assert.Equal(HttpStatusCode.NotFound, beforePublish.Status);

        var publish = await ReadAsync(await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null));
        Assert.Equal(HttpStatusCode.OK, publish.Status);
        Assert.Equal("published", publish.Root.GetProperty("data").GetProperty("status").GetString());

        var afterPublish = await ReadAsync(await anon.GetAsync($"/api/v1/articles/{slug}"));
        Assert.Equal(HttpStatusCode.OK, afterPublish.Status);
        Assert.Equal(1, afterPublish.Root.GetProperty("data").GetProperty("currentVersion").GetInt32());
    }

    [Fact]
    public async Task Authoring_requires_authentication()
    {
        var anon = AnonymousClient();
        var response = await anon.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload($"anon-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reader_lacks_permission_to_author()
    {
        var reader = await factory.CreateAuthenticatedClientAsync(Roles.Reader);
        var response = await reader.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload($"reader-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Created_article_records_the_authenticated_user_as_author()
    {
        var editor = await EditorClientAsync();
        var me = await ReadAsync(await editor.GetAsync("/api/v1/me"));
        var myId = me.Root.GetProperty("data").GetProperty("id").GetGuid();

        var create = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload($"author-{Guid.NewGuid():N}")));
        Assert.Equal(myId, create.Root.GetProperty("data").GetProperty("author").GetProperty("id").GetGuid());
    }

    // ---- Cross-module author resolution (ADR-0008). Content stores only an author id; the
    // display name is resolved through Identity's IUserDirectory implementation. ----

    [Fact]
    public async Task Public_read_resolves_the_author_display_name_through_the_user_directory()
    {
        var editor = await EditorClientAsync();
        var anon = AnonymousClient();
        var slug = $"byline-{Guid.NewGuid():N}";

        var create = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload(slug)));
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();
        (await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null)).EnsureSuccessStatusCode();

        var published = await ReadAsync(await anon.GetAsync($"/api/v1/articles/{slug}"));
        var author = published.Root.GetProperty("data").GetProperty("author");

        // CreateAuthenticatedClientAsync registers the user with the role name as the display name.
        Assert.Equal(Roles.Editor, author.GetProperty("displayName").GetString());
        Assert.NotEqual(Guid.Empty, author.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Detail_carries_the_author_bio_but_summaries_do_not()
    {
        // The bio is only useful on the article page's author card. Twenty summaries have no use
        // for twenty bios, and this is the cached public read path (see AuthorProfileDto).
        var editor = await EditorClientAsync();
        var anon = AnonymousClient();
        var slug = $"bio-{Guid.NewGuid():N}";

        var create = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload(slug)));
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();
        (await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null)).EnsureSuccessStatusCode();

        var detail = await ReadAsync(await anon.GetAsync($"/api/v1/articles/{slug}"));
        Assert.True(detail.Root.GetProperty("data").GetProperty("author").TryGetProperty("bio", out _));

        var list = await ReadAsync(await anon.GetAsync("/api/v1/articles"));
        var summary = list.Root.GetProperty("data").EnumerateArray()
            .Single(a => a.GetProperty("slug").GetString() == slug);

        Assert.False(summary.GetProperty("author").TryGetProperty("bio", out _));
    }

    [Fact]
    public async Task List_endpoint_resolves_authors_for_every_item()
    {
        var editor = await EditorClientAsync();
        var anon = AnonymousClient();
        var slug = $"list-byline-{Guid.NewGuid():N}";

        var create = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload(slug)));
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();
        (await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null)).EnsureSuccessStatusCode();

        var list = await ReadAsync(await anon.GetAsync("/api/v1/articles"));
        var mine = list.Root.GetProperty("data").EnumerateArray()
            .Single(a => a.GetProperty("slug").GetString() == slug);

        Assert.Equal(Roles.Editor, mine.GetProperty("author").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Status_and_visibility_cross_the_wire_lowercase()
    {
        var editor = await EditorClientAsync();
        var create = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload($"casing-{Guid.NewGuid():N}")));
        var data = create.Root.GetProperty("data");

        Assert.Equal("draft", data.GetProperty("status").GetString());
        Assert.Equal("public", data.GetProperty("visibility").GetString());
    }

    [Fact]
    public async Task Duplicate_slug_is_rejected()
    {
        var editor = await EditorClientAsync();
        var slug = $"dup-{Guid.NewGuid():N}";

        var first = await editor.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload(slug));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload(slug)));
        Assert.Equal(HttpStatusCode.Conflict, second.Status);
        Assert.Equal("slug_taken", second.Root.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Invalid_slug_fails_validation()
    {
        var editor = await EditorClientAsync();
        var response = await ReadAsync(
            await editor.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload("Not A Valid Slug!")));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("validation_failed", response.Root.GetProperty("error").GetProperty("code").GetString());
        Assert.True(response.Root.GetProperty("error").GetProperty("details").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Publishing_an_article_with_no_blocks_violates_a_business_rule()
    {
        var editor = await EditorClientAsync();
        var slug = $"no-blocks-{Guid.NewGuid():N}";

        var payload = new { title = "Blockless", summary = "", slug, content = new { version = 1, blocks = Array.Empty<object>() } };
        var create = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/articles", payload));
        Assert.Equal(HttpStatusCode.OK, create.Status);
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();

        var publish = await ReadAsync(await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, publish.Status);
        Assert.Equal("business_rule_violation", publish.Root.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unpublish_hides_from_public_read()
    {
        var editor = await EditorClientAsync();
        var anon = AnonymousClient();
        var slug = $"unpub-{Guid.NewGuid():N}";

        var create = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload(slug)));
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();

        await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/v1/articles/{slug}")).StatusCode);

        var unpublish = await editor.PostAsync($"/api/v1/authoring/articles/{id}/unpublish", null);
        Assert.Equal(HttpStatusCode.OK, unpublish.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/articles/{slug}")).StatusCode);
    }
}
