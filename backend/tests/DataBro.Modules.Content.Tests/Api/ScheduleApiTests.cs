using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataBro.Modules.Identity.Domain;
using Xunit;

namespace DataBro.Modules.Content.Tests.Api;

/// <summary>Scheduling endpoint behaviour (rules CT-4, CT-7). The sweep itself is unit-tested.</summary>
public class ScheduleApiTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    private Task<HttpClient> EditorClientAsync() => factory.CreateAuthenticatedClientAsync(Roles.Editor);
    private Task<HttpClient> AuthorClientAsync() => factory.CreateAuthenticatedClientAsync(Roles.Author);

    private static async Task<(HttpStatusCode Status, JsonElement Root)> ReadAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, JsonDocument.Parse(json).RootElement);
    }

    private static object DraftPayload(string slug, bool withContent = true) => new
    {
        title = "A Title",
        summary = "A summary",
        slug,
        content = new
        {
            version = 1,
            blocks = withContent
                ? new[] { new { id = "b0", type = "paragraph", data = new { text = "Body." } } }
                : [],
        },
    };

    private async Task<Guid> CreateDraftAsync(HttpClient client, bool withContent = true)
    {
        var response = await ReadAsync(await client.PostAsJsonAsync(
            "/api/v1/authoring/articles", DraftPayload($"sched-{Guid.NewGuid():N}", withContent)));
        Assert.Equal(HttpStatusCode.OK, response.Status);
        return response.Root.GetProperty("data").GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> ScheduleAsync(HttpClient client, Guid id, DateTimeOffset when)
        => client.PostAsJsonAsync($"/api/v1/authoring/articles/{id}/schedule", new { scheduledFor = when });

    [Fact]
    public async Task Scheduling_requires_content_publish()
    {
        var editor = await EditorClientAsync();
        var author = await AuthorClientAsync();
        var id = await CreateDraftAsync(editor);

        // An Author drafts; scheduling is a publishing act (CT-4).
        var response = await ScheduleAsync(author, id, DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Scheduling_a_future_time_sets_the_scheduled_state()
    {
        var editor = await EditorClientAsync();
        var id = await CreateDraftAsync(editor);
        var when = DateTimeOffset.UtcNow.AddDays(1);

        var response = await ReadAsync(await ScheduleAsync(editor, id, when));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("scheduled", response.Root.GetProperty("data").GetProperty("status").GetString());
        Assert.NotNull(response.Root.GetProperty("data").GetProperty("scheduledFor").GetString());
    }

    [Fact]
    public async Task Scheduling_in_the_past_is_rejected()
    {
        var editor = await EditorClientAsync();
        var id = await CreateDraftAsync(editor);

        var response = await ReadAsync(await ScheduleAsync(editor, id, DateTimeOffset.UtcNow.AddMinutes(-5)));

        // Domain rule: the time must be in the future.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
    }

    [Fact]
    public async Task Scheduling_an_empty_draft_is_rejected()
    {
        var editor = await EditorClientAsync();
        var id = await CreateDraftAsync(editor, withContent: false);

        var response = await ReadAsync(await ScheduleAsync(editor, id, DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
    }

    [Fact]
    public async Task A_missing_scheduled_time_is_a_400()
    {
        var editor = await EditorClientAsync();
        var id = await CreateDraftAsync(editor);

        var response = await editor.PostAsJsonAsync($"/api/v1/authoring/articles/{id}/schedule", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
