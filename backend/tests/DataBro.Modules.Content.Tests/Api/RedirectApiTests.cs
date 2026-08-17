using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataBro.Modules.Identity.Domain;
using Xunit;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Tests.Api;

/// <summary>
/// Slug-change → 301 redirect behaviour (rules CT-2/CT-3, docs/SEO.md §4). An indexed URL must never
/// silently 404 when a slug moves.
/// </summary>
public class RedirectApiTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    private Task<HttpClient> EditorClientAsync() => factory.CreateAuthenticatedClientAsync(Roles.Editor);
    private Task<HttpClient> AuthorClientAsync() => factory.CreateAuthenticatedClientAsync(Roles.Author);
    private HttpClient AnonymousClient() => factory.CreateClient();

    private static async Task<(HttpStatusCode Status, JsonElement Root)> ReadAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, JsonDocument.Parse(json).RootElement);
    }

    private static object DraftPayload(string slug) => new
    {
        title = "A Title",
        summary = "A summary",
        slug,
        content = new { version = 1, blocks = new[] { new { id = "b0", type = "paragraph", data = new { text = "Body." } } } },
    };

    private async Task<Guid> CreateArticleAsync(HttpClient client, string slug)
    {
        var response = await ReadAsync(await client.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload(slug)));
        Assert.Equal(HttpStatusCode.OK, response.Status);
        return response.Root.GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreatePublishedArticleAsync(HttpClient editor, string slug)
    {
        var id = await CreateArticleAsync(editor, slug);
        (await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null)).EnsureSuccessStatusCode();
        return id;
    }

    private static Task<HttpResponseMessage> ChangeSlugAsync(HttpClient client, string resource, Guid id, string slug)
        => client.PutAsJsonAsync($"/api/v1/authoring/{resource}/{id}/slug", new { slug });

    private async Task<Guid> CreateCategoryAsync(HttpClient editor, string slug)
    {
        var response = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/categories", new { name = slug, slug }));
        Assert.Equal(HttpStatusCode.OK, response.Status);
        return response.Root.GetProperty("data").GetProperty("id").GetGuid();
    }

    // ---- Authorization ----

    [Fact]
    public async Task Changing_an_article_slug_requires_content_publish()
    {
        var editor = await EditorClientAsync();
        var author = await AuthorClientAsync();
        var id = await CreatePublishedArticleAsync(editor, $"auth-{Guid.NewGuid():N}");

        // An Author drafts and edits, but changing a public URL is a publishing act (CT-4).
        Assert.Equal(HttpStatusCode.Forbidden, (await ChangeSlugAsync(author, "articles", id, "new-slug")).StatusCode);
    }

    // ---- CT-3: published article slug change writes a 301 ----

    [Fact]
    public async Task Renaming_a_published_article_records_a_301_and_frees_the_old_url()
    {
        var editor = await EditorClientAsync();
        var anon = AnonymousClient();
        var oldSlug = $"old-{Guid.NewGuid():N}";
        var newSlug = $"new-{Guid.NewGuid():N}";

        var id = await CreatePublishedArticleAsync(editor, oldSlug);
        Assert.Equal(HttpStatusCode.OK, (await ChangeSlugAsync(editor, "articles", id, newSlug)).StatusCode);

        // The new URL serves the article.
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/v1/articles/{newSlug}")).StatusCode);
        // The old URL no longer resolves as an article...
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/articles/{oldSlug}")).StatusCode);

        // ...but a redirect exists so the site serves a 301 rather than a dead page.
        var redirect = await ReadAsync(await anon.GetAsync($"/api/v1/redirects?from=/articles/{oldSlug}"));
        Assert.Equal(HttpStatusCode.OK, redirect.Status);
        Assert.Equal($"/articles/{newSlug}", redirect.Root.GetProperty("data").GetProperty("toPath").GetString());
        Assert.Equal(301, redirect.Root.GetProperty("data").GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Renaming_a_never_published_draft_records_no_redirect()
    {
        // A draft was never indexed, so there is no URL to protect — it simply moves.
        var editor = await EditorClientAsync();
        var anon = AnonymousClient();
        var oldSlug = $"draft-{Guid.NewGuid():N}";
        var newSlug = $"draft2-{Guid.NewGuid():N}";

        var id = await CreateArticleAsync(editor, oldSlug);
        Assert.Equal(HttpStatusCode.OK, (await ChangeSlugAsync(editor, "articles", id, newSlug)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/redirects?from=/articles/{oldSlug}")).StatusCode);
    }

    [Fact]
    public async Task A_slug_change_collapses_chains_to_a_single_hop()
    {
        var editor = await EditorClientAsync();
        var anon = AnonymousClient();
        var a = $"a-{Guid.NewGuid():N}";
        var b = $"b-{Guid.NewGuid():N}";
        var c = $"c-{Guid.NewGuid():N}";

        var id = await CreatePublishedArticleAsync(editor, a);
        await ChangeSlugAsync(editor, "articles", id, b);  // a -> b
        await ChangeSlugAsync(editor, "articles", id, c);  // b -> c, and a must repoint to c

        var fromA = await ReadAsync(await anon.GetAsync($"/api/v1/redirects?from=/articles/{a}"));
        var fromB = await ReadAsync(await anon.GetAsync($"/api/v1/redirects?from=/articles/{b}"));

        Assert.Equal($"/articles/{c}", fromA.Root.GetProperty("data").GetProperty("toPath").GetString());
        Assert.Equal($"/articles/{c}", fromB.Root.GetProperty("data").GetProperty("toPath").GetString());
    }

    // ---- Taxonomy slug changes are always protected (a term slug is always public) ----

    [Fact]
    public async Task Renaming_a_category_slug_records_a_301_and_resolves_at_the_new_slug()
    {
        var editor = await EditorClientAsync();
        var anon = AnonymousClient();
        var oldSlug = $"cat-{Guid.NewGuid():N}";
        var newSlug = $"cat2-{Guid.NewGuid():N}";

        var id = await CreateCategoryAsync(editor, oldSlug);
        Assert.Equal(HttpStatusCode.OK, (await ChangeSlugAsync(editor, "categories", id, newSlug)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/v1/categories/{newSlug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/categories/{oldSlug}")).StatusCode);

        var redirect = await ReadAsync(await anon.GetAsync($"/api/v1/redirects?from=/categories/{oldSlug}"));
        Assert.Equal($"/categories/{newSlug}", redirect.Root.GetProperty("data").GetProperty("toPath").GetString());
    }

    // ---- Guards ----

    [Fact]
    public async Task Changing_to_an_already_taken_slug_is_rejected()
    {
        var editor = await EditorClientAsync();
        var taken = $"taken-{Guid.NewGuid():N}";
        await CreatePublishedArticleAsync(editor, taken);
        var id = await CreatePublishedArticleAsync(editor, $"mover-{Guid.NewGuid():N}");

        var response = await ReadAsync(await ChangeSlugAsync(editor, "articles", id, taken));

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Equal("slug_taken", response.Root.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_invalid_slug_is_rejected()
    {
        var editor = await EditorClientAsync();
        var id = await CreatePublishedArticleAsync(editor, $"valid-{Guid.NewGuid():N}");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await ChangeSlugAsync(editor, "articles", id, "Not A Slug!")).StatusCode);
    }

    [Fact]
    public async Task An_unmatched_redirect_lookup_is_a_404()
    {
        var anon = AnonymousClient();
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await anon.GetAsync($"/api/v1/redirects?from=/articles/never-existed-{Guid.NewGuid():N}")).StatusCode);
    }
}
