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
/// Saved items.
///
/// The behaviour worth pinning is what happens when a saved thing stops being reachable: the row
/// survives with no path, rather than vanishing from a learner's list without explanation.
/// </summary>
public class BookmarkApiTests(LearningApiFactory factory) : IClassFixture<LearningApiFactory>
{
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private Task<HttpClient> LearnerAsync() => factory.CreateAuthenticatedClientAsync(Roles.Reader);

    private async Task<Guid> SeedBodyAsync(string title)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        var body = LessonContent.CreateDraft(
            Guid.NewGuid(), Slug.Create($"body-{Guid.NewGuid():N}"), title, "Summary",
            new ContentDocument
            {
                Version = 1,
                Blocks =
                [
                    new ContentBlock
                    {
                        Id = "b0",
                        Type = "paragraph",
                        Data = new System.Text.Json.Nodes.JsonObject { ["text"] = "Body." },
                    },
                ],
            });

        body.Publish(DateTimeOffset.UtcNow);
        db.LessonContents.Add(body);
        await db.SaveChangesAsync();

        return body.Id;
    }

    /// <summary>A published course with one lesson. Returns the editor, course id and lesson id.</summary>
    private async Task<(HttpClient Editor, Guid CourseId, Guid LessonId)> SeedCourseAsync(bool publish = true)
    {
        var editor = await factory.CreateAuthenticatedClientAsync(Roles.Editor);
        var slug = $"course-{Guid.NewGuid():N}";

        var created = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/courses",
            new { title = "Saveable Course", summary = "A course", slug }));
        var courseId = created.GetProperty("data").GetProperty("id").GetGuid();

        var withModule = await ReadAsync(await editor.PostAsJsonAsync(
            $"/api/v1/authoring/courses/{courseId}/modules", new { title = "Module" }));
        var moduleId = withModule.GetProperty("data").GetProperty("modules")[0].GetProperty("id").GetGuid();

        var withLesson = await ReadAsync(await editor.PostAsJsonAsync(
            $"/api/v1/authoring/courses/{courseId}/modules/{moduleId}/lessons",
            new { contentUnitId = await SeedBodyAsync("Saveable Lesson") }));
        var lessonId = withLesson.GetProperty("data").GetProperty("modules")[0]
            .GetProperty("lessons")[0].GetProperty("id").GetGuid();

        if (publish)
            (await editor.PostAsync($"/api/v1/authoring/courses/{courseId}/publish", null))
                .EnsureSuccessStatusCode();

        return (editor, courseId, lessonId);
    }

    [Fact]
    public async Task Saving_a_course_resolves_its_title_and_path()
    {
        var (_, courseId, _) = await SeedCourseAsync();
        var learner = await LearnerAsync();

        var data = (await ReadAsync(await learner.PostAsJsonAsync("/api/v1/me/bookmarks",
            new { kind = "course", targetId = courseId }))).GetProperty("data");

        Assert.Equal("course", data.GetProperty("kind").GetString());
        Assert.Equal("Saveable Course", data.GetProperty("title").GetString());
        Assert.StartsWith("/courses/", data.GetProperty("path").GetString());
    }

    [Fact]
    public async Task Saving_a_lesson_resolves_a_path_through_its_course()
    {
        // A lesson has no URL of its own — it is reachable only inside a course, so resolving one
        // means finding which course holds it.
        var (_, _, lessonId) = await SeedCourseAsync();
        var learner = await LearnerAsync();

        var data = (await ReadAsync(await learner.PostAsJsonAsync("/api/v1/me/bookmarks",
            new { kind = "lesson", targetId = lessonId }))).GetProperty("data");

        Assert.Equal("Saveable Lesson", data.GetProperty("title").GetString());
        Assert.Matches(@"^/courses/[^/]+/[^/]+$", data.GetProperty("path").GetString()!);
    }

    [Fact]
    public async Task Saving_twice_returns_the_same_bookmark_rather_than_a_conflict()
    {
        // A double-tapped bookmark button is not an error, exactly as a double-tapped enrol is not
        // (LN-9).
        var (_, courseId, _) = await SeedCourseAsync();
        var learner = await LearnerAsync();

        var first = await ReadAsync(await learner.PostAsJsonAsync("/api/v1/me/bookmarks",
            new { kind = "course", targetId = courseId }));
        var second = await learner.PostAsJsonAsync("/api/v1/me/bookmarks",
            new { kind = "course", targetId = courseId });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(
            first.GetProperty("data").GetProperty("id").GetGuid(),
            (await ReadAsync(second)).GetProperty("data").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task An_unpublished_target_keeps_its_title_and_loses_its_link()
    {
        // The row survives. Dropping it would make a learner's saved list shrink with no explanation
        // the moment an author unpublished something.
        var (editor, courseId, _) = await SeedCourseAsync();
        var learner = await LearnerAsync();

        (await learner.PostAsJsonAsync("/api/v1/me/bookmarks",
            new { kind = "course", targetId = courseId })).EnsureSuccessStatusCode();

        (await editor.PostAsync($"/api/v1/authoring/courses/{courseId}/unpublish", null))
            .EnsureSuccessStatusCode();

        var listed = (await ReadAsync(await learner.GetAsync("/api/v1/me/bookmarks")))
            .GetProperty("data").EnumerateArray()
            .First(b => b.GetProperty("targetId").GetGuid() == courseId);

        Assert.Equal("Saveable Course", listed.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, listed.GetProperty("path").ValueKind);
    }

    [Fact]
    public async Task Something_that_does_not_exist_cannot_be_saved()
    {
        // A bookmark pointing at nothing can only ever render as unavailable; refusing it is cheaper
        // than explaining it later.
        var learner = await LearnerAsync();

        var response = await learner.PostAsJsonAsync("/api/v1/me/bookmarks",
            new { kind = "course", targetId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_kind_is_refused()
    {
        var learner = await LearnerAsync();

        var response = await learner.PostAsJsonAsync("/api/v1/me/bookmarks",
            new { kind = "spaceship", targetId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Removing_works_and_removing_again_still_succeeds()
    {
        // Un-saving must never fail: a client that cannot complete it leaves the UI lying about
        // what is saved.
        var (_, courseId, _) = await SeedCourseAsync();
        var learner = await LearnerAsync();

        (await learner.PostAsJsonAsync("/api/v1/me/bookmarks",
            new { kind = "course", targetId = courseId })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK,
            (await learner.DeleteAsync($"/api/v1/me/bookmarks/course/{courseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await learner.DeleteAsync($"/api/v1/me/bookmarks/course/{courseId}")).StatusCode);

        var ids = (await ReadAsync(await learner.GetAsync("/api/v1/me/bookmarks/ids")))
            .GetProperty("data").EnumerateArray().Select(x => x.GetGuid()).ToList();

        Assert.DoesNotContain(courseId, ids);
    }

    [Fact]
    public async Task The_same_thing_can_be_saved_again_after_being_removed()
    {
        // Removal is a soft delete (XC-1), so the unique index is filtered on is_deleted — without
        // that filter the tombstone would block the row forever.
        var (_, courseId, _) = await SeedCourseAsync();
        var learner = await LearnerAsync();

        (await learner.PostAsJsonAsync("/api/v1/me/bookmarks",
            new { kind = "course", targetId = courseId })).EnsureSuccessStatusCode();
        (await learner.DeleteAsync($"/api/v1/me/bookmarks/course/{courseId}")).EnsureSuccessStatusCode();

        var again = await learner.PostAsJsonAsync("/api/v1/me/bookmarks",
            new { kind = "course", targetId = courseId });

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task One_learners_saved_list_is_invisible_to_another()
    {
        var (_, courseId, _) = await SeedCourseAsync();

        var alex = await LearnerAsync();
        (await alex.PostAsJsonAsync("/api/v1/me/bookmarks",
            new { kind = "course", targetId = courseId })).EnsureSuccessStatusCode();

        var sam = await LearnerAsync();
        var ids = (await ReadAsync(await sam.GetAsync("/api/v1/me/bookmarks/ids")))
            .GetProperty("data").EnumerateArray().Select(x => x.GetGuid()).ToList();

        Assert.DoesNotContain(courseId, ids);
    }

    [Fact]
    public async Task Bookmarks_reject_an_anonymous_caller()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().GetAsync("/api/v1/me/bookmarks")).StatusCode);
    }
}
