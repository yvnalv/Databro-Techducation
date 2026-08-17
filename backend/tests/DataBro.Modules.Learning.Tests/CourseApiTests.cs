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
/// The curriculum API end to end, including the join to lesson bodies that Content owns.
///
/// Runs against a real database and the real DI container, because the interesting behaviour is a
/// cross-module composition: Learning holds ids, Content holds bodies, and the reader is what makes
/// a course page possible without either module touching the other's tables.
/// </summary>
public class CourseApiTests(LearningApiFactory factory) : IClassFixture<LearningApiFactory>
{
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private Task<HttpClient> EditorAsync() => factory.CreateAuthenticatedClientAsync(Roles.Editor);

    /// <summary>Creates a lesson body in Content, optionally published, and returns its id.</summary>
    private async Task<Guid> SeedBodyAsync(string title, bool publish = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        var body = LessonContent.CreateDraft(
            Guid.NewGuid(), Slug.Create($"body-{Guid.NewGuid():N}"), title, "A lesson summary",
            new ContentDocument
            {
                Version = 1,
                Blocks =
                [
                    new ContentBlock
                    {
                        Id = "b0",
                        Type = "paragraph",
                        Data = new System.Text.Json.Nodes.JsonObject { ["text"] = $"Body of {title}." },
                    },
                ],
            });

        if (publish) body.Publish(DateTimeOffset.UtcNow);

        db.LessonContents.Add(body);
        await db.SaveChangesAsync();

        return body.Id;
    }

    /// <summary>A published course with one module holding the given bodies, in order.</summary>
    private async Task<(HttpClient Editor, Guid CourseId, string Slug)> SeedCourseAsync(
        params Guid[] bodyIds)
    {
        var editor = await EditorAsync();
        var slug = $"course-{Guid.NewGuid():N}";

        var created = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/courses", new
        {
            title = "RAG, End to End",
            summary = "A practical course",
            slug,
            difficulty = "intermediate",
        }));

        var courseId = created.GetProperty("data").GetProperty("id").GetGuid();

        var withModule = await ReadAsync(await editor.PostAsJsonAsync(
            $"/api/v1/authoring/courses/{courseId}/modules", new { title = "Retrieval" }));
        var moduleId = withModule.GetProperty("data").GetProperty("modules")[0].GetProperty("id").GetGuid();

        foreach (var bodyId in bodyIds)
        {
            var response = await editor.PostAsJsonAsync(
                $"/api/v1/authoring/courses/{courseId}/modules/{moduleId}/lessons",
                new { contentUnitId = bodyId });
            response.EnsureSuccessStatusCode();
        }

        return (editor, courseId, slug);
    }

    [Fact]
    public async Task A_course_page_joins_the_curriculum_to_its_bodies()
    {
        var body = await SeedBodyAsync("Chunking Strategies");
        var (editor, courseId, slug) = await SeedCourseAsync(body);

        await editor.PostAsync($"/api/v1/authoring/courses/{courseId}/publish", null);

        var page = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/courses/{slug}"));
        var lesson = page.GetProperty("data").GetProperty("modules")[0].GetProperty("lessons")[0];

        Assert.Equal("Chunking Strategies", lesson.GetProperty("title").GetString());
        Assert.True(lesson.GetProperty("isPublished").GetBoolean());
        Assert.Equal(
            "Body of Chunking Strategies.",
            lesson.GetProperty("blocks")[0].GetProperty("data").GetProperty("text").GetString());
    }

    [Fact]
    public async Task A_lesson_whose_body_is_unpublished_is_absent_from_the_public_page()
    {
        // ADR-0013: a course can go live before every lesson is written, and the unfinished ones
        // simply are not there.
        var published = await SeedBodyAsync("Ready", publish: true);
        var draft = await SeedBodyAsync("Not Ready", publish: false);
        var (editor, courseId, slug) = await SeedCourseAsync(published, draft);

        await editor.PostAsync($"/api/v1/authoring/courses/{courseId}/publish", null);

        var page = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/courses/{slug}"));
        var lessons = page.GetProperty("data").GetProperty("modules")[0].GetProperty("lessons");

        Assert.Equal(1, lessons.GetArrayLength());
        Assert.Equal("Ready", lessons[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task The_authoring_view_shows_the_unpublished_lesson_so_an_author_sees_the_gap()
    {
        // The other half of the same decision. Without this the affordance is a trap.
        var published = await SeedBodyAsync("Ready", publish: true);
        var draft = await SeedBodyAsync("Not Ready", publish: false);
        var (editor, courseId, _) = await SeedCourseAsync(published, draft);

        var view = await ReadAsync(await editor.GetAsync($"/api/v1/authoring/courses/{courseId}"));
        var lessons = view.GetProperty("data").GetProperty("modules")[0].GetProperty("lessons");

        Assert.Equal(2, lessons.GetArrayLength());
        Assert.False(lessons[1].GetProperty("isPublished").GetBoolean());
        Assert.Empty(lessons[1].GetProperty("blocks").EnumerateArray());
    }

    [Fact]
    public async Task An_unpublished_course_is_not_public()
    {
        var body = await SeedBodyAsync("Chunking");
        var (_, _, slug) = await SeedCourseAsync(body);

        var response = await factory.CreateClient().GetAsync($"/api/v1/courses/{slug}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_course_with_no_lessons_cannot_be_published()
    {
        var editor = await EditorAsync();
        var created = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/courses", new
        {
            title = "Empty Course",
            summary = "Nothing in it",
            slug = $"empty-{Guid.NewGuid():N}",
        }));

        var response = await editor.PostAsync(
            $"/api/v1/authoring/courses/{created.GetProperty("data").GetProperty("id").GetGuid()}/publish", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Reordering_lessons_is_one_call_and_renumbers_them()
    {
        var bodies = new[]
        {
            await SeedBodyAsync("One"),
            await SeedBodyAsync("Two"),
            await SeedBodyAsync("Three"),
        };
        var (editor, courseId, _) = await SeedCourseAsync(bodies);

        var before = await ReadAsync(await editor.GetAsync($"/api/v1/authoring/courses/{courseId}"));
        var module = before.GetProperty("data").GetProperty("modules")[0];
        var moduleId = module.GetProperty("id").GetGuid();
        var ids = module.GetProperty("lessons").EnumerateArray()
            .Select(l => l.GetProperty("id").GetGuid()).ToArray();

        var reordered = await ReadAsync(await editor.PutAsJsonAsync(
            $"/api/v1/authoring/courses/{courseId}/modules/{moduleId}/lessons/order",
            new { orderedIds = new[] { ids[2], ids[0], ids[1] } }));

        var after = reordered.GetProperty("data").GetProperty("modules")[0].GetProperty("lessons");

        Assert.Equal(["Three", "One", "Two"],
            after.EnumerateArray().Select(l => l.GetProperty("title").GetString()));
        Assert.Equal([0, 1, 2],
            after.EnumerateArray().Select(l => l.GetProperty("order").GetInt32()));
    }

    [Fact]
    public async Task Estimated_time_and_lesson_count_come_from_the_curriculum()
    {
        var bodies = new[] { await SeedBodyAsync("One"), await SeedBodyAsync("Two") };
        var (editor, courseId, _) = await SeedCourseAsync(bodies);

        var view = await ReadAsync(await editor.GetAsync($"/api/v1/authoring/courses/{courseId}"));
        var module = view.GetProperty("data").GetProperty("modules")[0];
        var moduleId = module.GetProperty("id").GetGuid();

        foreach (var lesson in module.GetProperty("lessons").EnumerateArray())
        {
            await editor.PatchAsJsonAsync(
                $"/api/v1/authoring/courses/{courseId}/modules/{moduleId}/lessons/{lesson.GetProperty("id").GetGuid()}",
                new { estimatedMinutes = 25, difficulty = "advanced", objectives = new[] { "Do the thing" } });
        }

        var updated = await ReadAsync(await editor.GetAsync($"/api/v1/authoring/courses/{courseId}"));

        Assert.Equal(2, updated.GetProperty("data").GetProperty("lessonCount").GetInt32());
        Assert.Equal(50, updated.GetProperty("data").GetProperty("estimatedMinutes").GetInt32());
    }

    [Fact]
    public async Task Authoring_a_curriculum_requires_permission()
    {
        var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/v1/authoring/courses", new
        {
            title = "Anonymous",
            summary = "Should not work",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_author_may_build_a_curriculum_but_not_publish_it()
    {
        // The same split articles use (CT-4): structure is editing, going live is publishing.
        var body = await SeedBodyAsync("Chunking");
        var (_, courseId, _) = await SeedCourseAsync(body);

        var author = await factory.CreateAuthenticatedClientAsync(Roles.Author);

        var edit = await author.PostAsJsonAsync(
            $"/api/v1/authoring/courses/{courseId}/modules", new { title = "Added by an author" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var publish = await author.PostAsync($"/api/v1/authoring/courses/{courseId}/publish", null);
        Assert.Equal(HttpStatusCode.Forbidden, publish.StatusCode);
    }
}
