using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataBro.Modules.Identity.Domain;
using Xunit;

namespace DataBro.Modules.Content.Tests.Api;

public class TaxonomyApiTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    private Task<HttpClient> EditorClientAsync() => factory.CreateAuthenticatedClientAsync(Roles.Editor);
    private Task<HttpClient> AuthorClientAsync() => factory.CreateAuthenticatedClientAsync(Roles.Author);
    private HttpClient AnonymousClient() => factory.CreateClient();

    private static async Task<(HttpStatusCode Status, JsonElement Root)> ReadAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, JsonDocument.Parse(json).RootElement);
    }

    private static object DraftPayload(string slug, Guid? categoryId = null, Guid[]? tagIds = null) => new
    {
        title = "A Title",
        summary = "A summary",
        slug,
        content = new { version = 1, blocks = new[] { new { id = "b0", type = "paragraph", data = new { text = "Body." } } } },
        categoryId,
        tagIds,
    };

    private async Task<Guid> CreateCategoryAsync(HttpClient editor, string slug, Guid? parentId = null)
    {
        var response = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/categories", new { name = slug, slug, parentId }));
        Assert.Equal(HttpStatusCode.OK, response.Status);
        return response.Root.GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateTagAsync(HttpClient editor, string slug)
    {
        var response = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/tags", new { name = slug, slug }));
        Assert.Equal(HttpStatusCode.OK, response.Status);
        return response.Root.GetProperty("data").GetProperty("id").GetGuid();
    }

    // ---- Authorization ----

    [Fact]
    public async Task Creating_a_category_requires_taxonomy_manage()
    {
        var anon = AnonymousClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anon.PostAsJsonAsync("/api/v1/authoring/categories", new { name = "x" })).StatusCode);
    }

    [Fact]
    public async Task An_author_may_not_create_taxonomy_terms()
    {
        // Author holds Content.Create/Edit but not Taxonomy.Manage: they may label an article with
        // existing terms, but cannot mint new vocabulary. This is what stops tag sprawl.
        var author = await AuthorClientAsync();

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await author.PostAsJsonAsync("/api/v1/authoring/tags", new { name = "adhoc" })).StatusCode);
    }

    [Fact]
    public async Task An_author_may_assign_existing_terms_to_their_article()
    {
        var editor = await EditorClientAsync();
        var author = await AuthorClientAsync();

        var categoryId = await CreateCategoryAsync(editor, $"cat-{Guid.NewGuid():N}");
        var tagId = await CreateTagAsync(editor, $"tag-{Guid.NewGuid():N}");

        var create = await ReadAsync(await author.PostAsJsonAsync(
            "/api/v1/authoring/articles", DraftPayload($"a-{Guid.NewGuid():N}", categoryId, [tagId])));

        Assert.Equal(HttpStatusCode.OK, create.Status);
        Assert.Equal(categoryId, create.Root.GetProperty("data").GetProperty("category").GetProperty("id").GetGuid());
        Assert.Single(create.Root.GetProperty("data").GetProperty("tags").EnumerateArray());
    }

    // ---- TX-1: slug uniqueness within a type ----

    [Fact]
    public async Task Duplicate_category_slug_is_rejected()
    {
        var editor = await EditorClientAsync();
        var slug = $"dup-{Guid.NewGuid():N}";

        await CreateCategoryAsync(editor, slug);
        var second = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/categories", new { name = slug, slug }));

        Assert.Equal(HttpStatusCode.Conflict, second.Status);
    }

    [Fact]
    public async Task A_category_and_a_tag_may_share_a_slug()
    {
        // TX-1 is per type: /categories/python and /tags/python are different pages by design.
        var editor = await EditorClientAsync();
        var slug = $"shared-{Guid.NewGuid():N}";

        await CreateCategoryAsync(editor, slug);
        var tag = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/tags", new { name = slug, slug }));

        Assert.Equal(HttpStatusCode.OK, tag.Status);
    }

    // ---- TX-2: a category in use cannot be deleted ----

    [Fact]
    public async Task A_category_still_classifying_articles_cannot_be_deleted()
    {
        var editor = await EditorClientAsync();
        var categoryId = await CreateCategoryAsync(editor, $"inuse-{Guid.NewGuid():N}");

        await editor.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload($"a-{Guid.NewGuid():N}", categoryId));

        var delete = await ReadAsync(await editor.DeleteAsync($"/api/v1/authoring/categories/{categoryId}"));

        Assert.Equal(HttpStatusCode.Conflict, delete.Status);
        // The message names the count so an editor knows how much reassignment is pending.
        Assert.Contains("1 article", delete.Root.GetProperty("error").GetProperty("message").GetString()!);
    }

    [Fact]
    public async Task A_category_with_children_cannot_be_deleted()
    {
        var editor = await EditorClientAsync();
        var parentId = await CreateCategoryAsync(editor, $"parent-{Guid.NewGuid():N}");
        await CreateCategoryAsync(editor, $"child-{Guid.NewGuid():N}", parentId);

        var delete = await ReadAsync(await editor.DeleteAsync($"/api/v1/authoring/categories/{parentId}"));

        Assert.Equal(HttpStatusCode.Conflict, delete.Status);
    }

    [Fact]
    public async Task An_unused_category_can_be_deleted()
    {
        var editor = await EditorClientAsync();
        var categoryId = await CreateCategoryAsync(editor, $"unused-{Guid.NewGuid():N}");

        Assert.Equal(
            HttpStatusCode.OK,
            (await editor.DeleteAsync($"/api/v1/authoring/categories/{categoryId}")).StatusCode);
    }

    // ---- TX-3: cycles ----

    [Fact]
    public async Task A_category_cannot_be_moved_beneath_its_own_descendant()
    {
        var editor = await EditorClientAsync();
        var rootId = await CreateCategoryAsync(editor, $"root-{Guid.NewGuid():N}");
        var childId = await CreateCategoryAsync(editor, $"kid-{Guid.NewGuid():N}", rootId);

        var move = await ReadAsync(await editor.PatchAsJsonAsync(
            $"/api/v1/authoring/categories/{rootId}", new { name = "Root", parentId = childId }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, move.Status);
    }

    // ---- Assignment validation ----

    [Fact]
    public async Task Assigning_a_nonexistent_category_is_rejected()
    {
        var editor = await EditorClientAsync();

        var create = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/articles", DraftPayload($"a-{Guid.NewGuid():N}", Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.BadRequest, create.Status);
    }

    [Fact]
    public async Task Assigning_a_nonexistent_tag_is_rejected_rather_than_silently_dropped()
    {
        var editor = await EditorClientAsync();

        var create = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/articles", DraftPayload($"a-{Guid.NewGuid():N}", null, [Guid.NewGuid()])));

        Assert.Equal(HttpStatusCode.BadRequest, create.Status);
    }

    // ---- Soft-deleted terms must not leak onto public pages ----

    [Fact]
    public async Task A_deleted_tag_disappears_from_a_published_article()
    {
        var editor = await EditorClientAsync();
        var anon = AnonymousClient();
        var slug = $"deltag-{Guid.NewGuid():N}";

        var keepId = await CreateTagAsync(editor, $"keep-{Guid.NewGuid():N}");
        var dropId = await CreateTagAsync(editor, $"drop-{Guid.NewGuid():N}");

        var create = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/articles", DraftPayload(slug, null, [keepId, dropId])));
        var articleId = create.Root.GetProperty("data").GetProperty("id").GetGuid();
        await editor.PostAsync($"/api/v1/authoring/articles/{articleId}/publish", null);

        var before = await ReadAsync(await anon.GetAsync($"/api/v1/articles/{slug}"));
        Assert.Equal(2, before.Root.GetProperty("data").GetProperty("tags").GetArrayLength());

        await editor.DeleteAsync($"/api/v1/authoring/tags/{dropId}");

        var after = await ReadAsync(await anon.GetAsync($"/api/v1/articles/{slug}"));
        var tags = after.Root.GetProperty("data").GetProperty("tags").EnumerateArray().ToList();

        Assert.Single(tags);
        Assert.Equal(keepId, tags[0].GetProperty("id").GetGuid());
    }

    // ---- Public filtering and paging ----

    [Fact]
    public async Task Public_list_can_be_filtered_by_category_slug()
    {
        var editor = await EditorClientAsync();
        var anon = AnonymousClient();
        var categorySlug = $"filter-{Guid.NewGuid():N}";
        var categoryId = await CreateCategoryAsync(editor, categorySlug);
        var articleSlug = $"in-cat-{Guid.NewGuid():N}";

        var create = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/articles", DraftPayload(articleSlug, categoryId)));
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();
        await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null);

        var filtered = await ReadAsync(await anon.GetAsync($"/api/v1/articles?category={categorySlug}"));
        var items = filtered.Root.GetProperty("data").EnumerateArray().ToList();

        Assert.Single(items);
        Assert.Equal(articleSlug, items[0].GetProperty("slug").GetString());
        Assert.Equal(1, filtered.Root.GetProperty("meta").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task An_unknown_filter_slug_returns_an_empty_page_not_every_article()
    {
        // Silently ignoring an unmatched filter would serve the whole catalogue on a category page
        // that should 404/empty — worse than useless for both readers and crawlers.
        var anon = AnonymousClient();

        var response = await ReadAsync(await anon.GetAsync("/api/v1/articles?category=no-such-category"));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Empty(response.Root.GetProperty("data").EnumerateArray());
        Assert.Equal(0, response.Root.GetProperty("meta").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Public_list_exposes_paging_meta_and_respects_page_size()
    {
        var editor = await EditorClientAsync();
        var anon = AnonymousClient();
        var tagSlug = $"paged-{Guid.NewGuid():N}";
        var tagId = await CreateTagAsync(editor, tagSlug);

        for (var i = 0; i < 3; i++)
        {
            var create = await ReadAsync(await editor.PostAsJsonAsync(
                "/api/v1/authoring/articles", DraftPayload($"p{i}-{Guid.NewGuid():N}", null, [tagId])));
            var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();
            await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null);
        }

        var first = await ReadAsync(await anon.GetAsync($"/api/v1/articles?tag={tagSlug}&page=1&pageSize=2"));
        var meta = first.Root.GetProperty("meta");

        Assert.Equal(2, first.Root.GetProperty("data").GetArrayLength());
        Assert.Equal(3, meta.GetProperty("total").GetInt32());
        Assert.Equal(2, meta.GetProperty("totalPages").GetInt32());

        var second = await ReadAsync(await anon.GetAsync($"/api/v1/articles?tag={tagSlug}&page=2&pageSize=2"));
        Assert.Equal(1, second.Root.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task Page_size_is_clamped_so_it_cannot_be_used_to_pull_the_whole_table()
    {
        var anon = AnonymousClient();

        var response = await ReadAsync(await anon.GetAsync("/api/v1/articles?pageSize=100000"));

        Assert.Equal(100, response.Root.GetProperty("meta").GetProperty("pageSize").GetInt32());
    }

    // ---- Public taxonomy reads ----

    [Fact]
    public async Task Category_by_slug_returns_its_ancestor_trail_for_breadcrumbs()
    {
        var editor = await EditorClientAsync();
        var anon = AnonymousClient();

        var rootSlug = $"ai-{Guid.NewGuid():N}";
        var midSlug = $"ml-{Guid.NewGuid():N}";
        var leafSlug = $"dl-{Guid.NewGuid():N}";

        var rootId = await CreateCategoryAsync(editor, rootSlug);
        var midId = await CreateCategoryAsync(editor, midSlug, rootId);
        await CreateCategoryAsync(editor, leafSlug, midId);

        var response = await ReadAsync(await anon.GetAsync($"/api/v1/categories/{leafSlug}"));
        var ancestors = response.Root.GetProperty("data").GetProperty("ancestors").EnumerateArray().ToList();

        // Root first, so the client can render the trail without reversing it.
        Assert.Equal(2, ancestors.Count);
        Assert.Equal(rootSlug, ancestors[0].GetProperty("slug").GetString());
        Assert.Equal(midSlug, ancestors[1].GetProperty("slug").GetString());
    }

    [Fact]
    public async Task Category_list_counts_published_articles_only()
    {
        // The tile count must reflect what a reader can actually open, so a draft must not inflate
        // it. This is deliberately a different count from the one guarding TX-2 deletion.
        var editor = await EditorClientAsync();
        var anon = AnonymousClient();
        var slug = $"counted-{Guid.NewGuid():N}";
        var categoryId = await CreateCategoryAsync(editor, slug);

        var draft = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/articles", DraftPayload($"d-{Guid.NewGuid():N}", categoryId)));
        var publishedArticle = await ReadAsync(await editor.PostAsJsonAsync(
            "/api/v1/authoring/articles", DraftPayload($"p-{Guid.NewGuid():N}", categoryId)));

        var publishedId = publishedArticle.Root.GetProperty("data").GetProperty("id").GetGuid();
        (await editor.PostAsync($"/api/v1/authoring/articles/{publishedId}/publish", null))
            .EnsureSuccessStatusCode();

        var list = await ReadAsync(await anon.GetAsync("/api/v1/categories"));
        var mine = list.Root.GetProperty("data").EnumerateArray()
            .Single(c => c.GetProperty("slug").GetString() == slug);

        // Two articles exist in the category; only one is published.
        Assert.Equal(1, mine.GetProperty("articleCount").GetInt32());
        Assert.NotEqual(Guid.Empty, draft.Root.GetProperty("data").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task An_unknown_category_slug_is_a_404()
    {
        var anon = AnonymousClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/v1/categories/nope")).StatusCode);
    }
}
