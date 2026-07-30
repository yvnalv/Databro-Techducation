using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DataBro.Modules.Content.Tests.Api;

public class ContentApiTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    private HttpClient Client => factory.CreateClient();

    private static object DraftPayload(string slug, string title = "A Title", params string[] blockTexts)
    {
        var blocks = (blockTexts.Length == 0 ? ["Some body text."] : blockTexts)
            .Select((t, i) => new { id = $"b{i}", type = "paragraph", data = new { text = t } })
            .ToArray();

        return new
        {
            title,
            summary = "A summary",
            slug,
            content = new { version = 1, blocks },
        };
    }

    private static async Task<(HttpStatusCode Status, JsonElement Root)> ReadAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, JsonDocument.Parse(json).RootElement);
    }

    [Fact]
    public async Task Create_publish_and_read_public_happy_path()
    {
        var client = Client;
        var slug = $"happy-path-{Guid.NewGuid():N}";

        // create draft
        var create = await ReadAsync(await client.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload(slug)));
        Assert.Equal(HttpStatusCode.OK, create.Status);
        Assert.True(create.Root.GetProperty("success").GetBoolean());
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();

        // public read before publish -> 404
        var beforePublish = await ReadAsync(await client.GetAsync($"/api/v1/articles/{slug}"));
        Assert.Equal(HttpStatusCode.NotFound, beforePublish.Status);
        Assert.Equal("not_found", beforePublish.Root.GetProperty("error").GetProperty("code").GetString());

        // publish
        var publish = await ReadAsync(await client.PostAsync($"/api/v1/authoring/articles/{id}/publish", null));
        Assert.Equal(HttpStatusCode.OK, publish.Status);
        Assert.Equal("Published", publish.Root.GetProperty("data").GetProperty("status").GetString());

        // public read after publish -> 200
        var afterPublish = await ReadAsync(await client.GetAsync($"/api/v1/articles/{slug}"));
        Assert.Equal(HttpStatusCode.OK, afterPublish.Status);
        Assert.Equal(1, afterPublish.Root.GetProperty("data").GetProperty("currentVersion").GetInt32());
    }

    [Fact]
    public async Task Duplicate_slug_is_rejected()
    {
        var client = Client;
        var slug = $"dup-{Guid.NewGuid():N}";

        var first = await client.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload(slug));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await ReadAsync(await client.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload(slug)));
        Assert.Equal(HttpStatusCode.Conflict, second.Status);
        Assert.Equal("slug_taken", second.Root.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Invalid_slug_fails_validation()
    {
        var response = await ReadAsync(
            await Client.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload("Not A Valid Slug!")));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("validation_failed", response.Root.GetProperty("error").GetProperty("code").GetString());
        Assert.True(response.Root.GetProperty("error").GetProperty("details").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Empty_title_fails_validation()
    {
        var payload = DraftPayload($"empty-title-{Guid.NewGuid():N}", title: "");
        var response = await ReadAsync(await Client.PostAsJsonAsync("/api/v1/authoring/articles", payload));

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("validation_failed", response.Root.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Publishing_an_article_with_no_blocks_violates_a_business_rule()
    {
        var client = Client;
        var slug = $"no-blocks-{Guid.NewGuid():N}";

        // An empty block list is a valid payload shape, but the domain forbids publishing it (CT-1).
        var payload = new
        {
            title = "Blockless",
            summary = "",
            slug,
            content = new { version = 1, blocks = Array.Empty<object>() },
        };

        var create = await ReadAsync(await client.PostAsJsonAsync("/api/v1/authoring/articles", payload));
        Assert.Equal(HttpStatusCode.OK, create.Status);
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();

        var publish = await ReadAsync(await client.PostAsync($"/api/v1/authoring/articles/{id}/publish", null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, publish.Status);
        Assert.Equal("business_rule_violation", publish.Root.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unpublish_hides_from_public_read()
    {
        var client = Client;
        var slug = $"unpub-{Guid.NewGuid():N}";

        var create = await ReadAsync(await client.PostAsJsonAsync("/api/v1/authoring/articles", DraftPayload(slug)));
        var id = create.Root.GetProperty("data").GetProperty("id").GetGuid();

        await client.PostAsync($"/api/v1/authoring/articles/{id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/articles/{slug}")).StatusCode);

        var unpublish = await client.PostAsync($"/api/v1/authoring/articles/{id}/unpublish", null);
        Assert.Equal(HttpStatusCode.OK, unpublish.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/articles/{slug}")).StatusCode);
    }
}
