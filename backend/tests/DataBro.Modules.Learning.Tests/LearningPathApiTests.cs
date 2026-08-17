using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataBro.Modules.Content.Domain;
using DataBro.Modules.Content.Infrastructure.Persistence;
using DataBro.Modules.Identity.Domain;
using DataBro.Platform.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataBro.Modules.Learning.Tests;

/// <summary>
/// Learning paths end to end: curating a sequence of courses, and the read that resolves those ids
/// into cards.
///
/// The interesting behaviour is what a path does when the courses under it are not ready — it holds
/// ids it does not own, so the two can always disagree.
/// </summary>
public class LearningPathApiTests(LearningApiFactory factory) : IClassFixture<LearningApiFactory>
{
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private Task<HttpClient> EditorAsync() => factory.CreateAuthenticatedClientAsync(Roles.Editor);

    /// <summary>A course with one published lesson, published unless told otherwise.</summary>
    private async Task<(Guid Id, string Slug)> SeedCourseAsync(
        HttpClient editor, string title, bool publish = true)
    {
        Guid bodyId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            var body = LessonContent.CreateDraft(
                Guid.NewGuid(), Slug.Create($"body-{Guid.NewGuid():N}"), $"{title} lesson", "Summary",
                new ContentDocument
                {
                    Version = 1,
                    Blocks = [new ContentBlock { Id = "b0", Type = "paragraph", Data = new System.Text.Json.Nodes.JsonObject { ["text"] = "Body." } }],
                });
            body.Publish(DateTimeOffset.UtcNow);
            db.LessonContents.Add(body);
            await db.SaveChangesAsync();
            bodyId = body.Id;
        }

        var slug = $"course-{Guid.NewGuid():N}";
        var created = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/courses",
            new { title, summary = "A course", slug }));
        var courseId = created.GetProperty("data").GetProperty("id").GetGuid();

        var withModule = await ReadAsync(await editor.PostAsJsonAsync(
            $"/api/v1/authoring/courses/{courseId}/modules", new { title = "Module" }));
        var moduleId = withModule.GetProperty("data").GetProperty("modules")[0].GetProperty("id").GetGuid();

        (await editor.PostAsJsonAsync(
            $"/api/v1/authoring/courses/{courseId}/modules/{moduleId}/lessons",
            new { contentUnitId = bodyId })).EnsureSuccessStatusCode();

        if (publish)
            (await editor.PostAsync($"/api/v1/authoring/courses/{courseId}/publish", null))
                .EnsureSuccessStatusCode();

        return (courseId, slug);
    }

    private async Task<(HttpClient Editor, Guid PathId, string Slug)> SeedPathAsync(params Guid[] courseIds)
    {
        var editor = await EditorAsync();
        var slug = $"path-{Guid.NewGuid():N}";

        var created = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/learning-paths",
            new { title = "Become an LLM Engineer", summary = "A curated track", slug }));
        var pathId = created.GetProperty("data").GetProperty("id").GetGuid();

        foreach (var courseId in courseIds)
            (await editor.PostAsync($"/api/v1/authoring/learning-paths/{pathId}/courses/{courseId}", null))
                .EnsureSuccessStatusCode();

        return (editor, pathId, slug);
    }

    private static string[] Titles(JsonElement data) =>
        data.GetProperty("courses").EnumerateArray()
            .Select(c => c.GetProperty("title").GetString()!)
            .ToArray();

    [Fact]
    public async Task A_path_resolves_its_course_ids_into_cards_in_the_curated_order()
    {
        var editor = await EditorAsync();
        var first = await SeedCourseAsync(editor, "Python Foundations");
        var second = await SeedCourseAsync(editor, "LLM Engineering");

        var (curator, pathId, slug) = await SeedPathAsync(first.Id, second.Id);
        (await curator.PostAsync($"/api/v1/authoring/learning-paths/{pathId}/publish", null))
            .EnsureSuccessStatusCode();

        var page = (await ReadAsync(await factory.CreateClient()
            .GetAsync($"/api/v1/learning-paths/{slug}"))).GetProperty("data");

        Assert.Equal(["Python Foundations", "LLM Engineering"], Titles(page));
    }

    [Fact]
    public async Task Reordering_a_path_reorders_the_cards()
    {
        // The sequence is the entire point of a path, so the order must come from the path and not
        // from whatever order the repository happened to return the courses in.
        var editor = await EditorAsync();
        var first = await SeedCourseAsync(editor, "Alpha");
        var second = await SeedCourseAsync(editor, "Beta");

        var (curator, pathId, slug) = await SeedPathAsync(first.Id, second.Id);

        (await curator.PutAsJsonAsync($"/api/v1/authoring/learning-paths/{pathId}/courses/order",
            new { orderedIds = new[] { second.Id, first.Id } })).EnsureSuccessStatusCode();

        (await curator.PostAsync($"/api/v1/authoring/learning-paths/{pathId}/publish", null))
            .EnsureSuccessStatusCode();

        var page = (await ReadAsync(await factory.CreateClient()
            .GetAsync($"/api/v1/learning-paths/{slug}"))).GetProperty("data");

        Assert.Equal(["Beta", "Alpha"], Titles(page));
    }

    [Fact]
    public async Task An_unpublished_course_is_dropped_from_the_public_path_but_kept_for_the_curator()
    {
        // A path is curated ahead of the courses in it — the same affordance a course has over its
        // lessons (LN-1/LN-2). The learner sees what is ready; the curator sees the gap.
        var editor = await EditorAsync();
        var ready = await SeedCourseAsync(editor, "Ready", publish: true);
        var notReady = await SeedCourseAsync(editor, "Not Ready", publish: false);

        var (curator, pathId, slug) = await SeedPathAsync(ready.Id, notReady.Id);
        (await curator.PostAsync($"/api/v1/authoring/learning-paths/{pathId}/publish", null))
            .EnsureSuccessStatusCode();

        var publicPage = (await ReadAsync(await factory.CreateClient()
            .GetAsync($"/api/v1/learning-paths/{slug}"))).GetProperty("data");
        Assert.Equal(["Ready"], Titles(publicPage));

        var authoring = (await ReadAsync(await curator
            .GetAsync($"/api/v1/authoring/learning-paths/{pathId}"))).GetProperty("data");
        Assert.Equal(["Ready", "Not Ready"], Titles(authoring));
    }

    [Fact]
    public async Task An_unpublished_path_is_a_404()
    {
        var editor = await EditorAsync();
        var course = await SeedCourseAsync(editor, "Something");
        var (_, _, slug) = await SeedPathAsync(course.Id);
        // Deliberately not published.

        Assert.Equal(HttpStatusCode.NotFound,
            (await factory.CreateClient().GetAsync($"/api/v1/learning-paths/{slug}")).StatusCode);
    }

    [Fact]
    public async Task An_empty_path_cannot_be_published()
    {
        var (curator, pathId, _) = await SeedPathAsync();

        var response = await curator.PostAsync($"/api/v1/authoring/learning-paths/{pathId}/publish", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Adding_the_same_course_twice_is_a_no_op_rather_than_a_duplicate()
    {
        // A builder UI dropping the same card twice is a slip, not a decision worth refusing.
        var editor = await EditorAsync();
        var course = await SeedCourseAsync(editor, "Only Once");

        var (curator, pathId, _) = await SeedPathAsync(course.Id);
        var again = await ReadAsync(await curator.PostAsync(
            $"/api/v1/authoring/learning-paths/{pathId}/courses/{course.Id}", null));

        Assert.Single(again.GetProperty("data").GetProperty("courses").EnumerateArray());
    }

    [Fact]
    public async Task Curating_a_path_requires_an_editorial_permission()
    {
        var editor = await EditorAsync();
        var course = await SeedCourseAsync(editor, "Guarded");
        var (_, pathId, _) = await SeedPathAsync(course.Id);

        var learner = await factory.CreateAuthenticatedClientAsync(Roles.Reader);
        var response = await learner.PostAsync(
            $"/api/v1/authoring/learning-paths/{pathId}/courses/{course.Id}", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
