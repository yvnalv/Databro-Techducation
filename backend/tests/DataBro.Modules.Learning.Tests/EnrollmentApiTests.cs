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
/// Enrollment and progress end to end — the platform's first write-heavy surface.
///
/// The behaviour worth testing here is not "a checkbox persists". It is the set of rules that decide
/// what a learner is allowed to record, and the one rule that has to survive the curriculum changing
/// underneath it (LN-6).
/// </summary>
public class EnrollmentApiTests(LearningApiFactory factory) : IClassFixture<LearningApiFactory>
{
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private Task<HttpClient> LearnerAsync() => factory.CreateAuthenticatedClientAsync(Roles.Reader);

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
    private async Task<(HttpClient Editor, Guid CourseId, Guid ModuleId, string Slug)> SeedPublishedCourseAsync(
        params Guid[] bodyIds)
    {
        var editor = await factory.CreateAuthenticatedClientAsync(Roles.Editor);
        var slug = $"course-{Guid.NewGuid():N}";

        var created = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/courses", new
        {
            title = "Progress Course",
            summary = "A course to walk through",
            slug,
        }));
        var courseId = created.GetProperty("data").GetProperty("id").GetGuid();

        var withModule = await ReadAsync(await editor.PostAsJsonAsync(
            $"/api/v1/authoring/courses/{courseId}/modules", new { title = "Module One" }));
        var moduleId = withModule.GetProperty("data").GetProperty("modules")[0].GetProperty("id").GetGuid();

        foreach (var bodyId in bodyIds)
        {
            (await editor.PostAsJsonAsync(
                $"/api/v1/authoring/courses/{courseId}/modules/{moduleId}/lessons",
                new { contentUnitId = bodyId })).EnsureSuccessStatusCode();
        }

        (await editor.PostAsync($"/api/v1/authoring/courses/{courseId}/publish", null))
            .EnsureSuccessStatusCode();

        return (editor, courseId, moduleId, slug);
    }

    /// <summary>The lesson ids on the public course page, in curriculum order.</summary>
    private async Task<Guid[]> PublicLessonIdsAsync(string slug)
    {
        var page = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/courses/{slug}"));
        return page.GetProperty("data").GetProperty("modules")
            .EnumerateArray()
            .SelectMany(m => m.GetProperty("lessons").EnumerateArray())
            .Select(l => l.GetProperty("id").GetGuid())
            .ToArray();
    }

    // ---- Enrolling ----

    [Fact]
    public async Task Enrolling_twice_returns_the_same_enrollment_rather_than_a_conflict()
    {
        // A double-tapped button is not an error. If this 409s, every client has to special-case a
        // failure that means "it worked".
        var (_, _, _, slug) = await SeedPublishedCourseAsync(await SeedBodyAsync("One"));
        var learner = await LearnerAsync();

        var first = await ReadAsync(await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null));
        var second = await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(
            first.GetProperty("data").GetProperty("id").GetGuid(),
            (await ReadAsync(second)).GetProperty("data").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Enrolling_in_an_unpublished_course_is_a_404()
    {
        var editor = await factory.CreateAuthenticatedClientAsync(Roles.Editor);
        var slug = $"draft-{Guid.NewGuid():N}";
        await editor.PostAsJsonAsync("/api/v1/authoring/courses",
            new { title = "Still A Draft", summary = "Not live", slug });

        var learner = await LearnerAsync();
        var response = await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Progress_endpoints_reject_an_anonymous_caller()
    {
        var (_, _, _, slug) = await SeedPublishedCourseAsync(await SeedBodyAsync("One"));

        var response = await factory.CreateClient().PostAsync($"/api/v1/me/enrollments/{slug}", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Recording progress ----

    [Fact]
    public async Task Completing_a_lesson_advances_the_count_and_the_resume_point()
    {
        var (_, _, _, slug) = await SeedPublishedCourseAsync(
            await SeedBodyAsync("One"), await SeedBodyAsync("Two"));
        var lessons = await PublicLessonIdsAsync(slug);

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);

        var data = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{lessons[0]}/complete", null))).GetProperty("data");

        Assert.Equal(1, data.GetProperty("completedLessons").GetInt32());
        Assert.Equal(2, data.GetProperty("totalLessons").GetInt32());
        Assert.Equal(50, data.GetProperty("percentComplete").GetInt32());
        Assert.Equal(lessons[0], data.GetProperty("lastLessonId").GetGuid());
        Assert.True(data.GetProperty("completedAt").ValueKind is JsonValueKind.Null);
    }

    [Fact]
    public async Task Completing_the_same_lesson_twice_does_not_double_count_it()
    {
        var (_, _, _, slug) = await SeedPublishedCourseAsync(
            await SeedBodyAsync("One"), await SeedBodyAsync("Two"));
        var lessons = await PublicLessonIdsAsync(slug);

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}/lessons/{lessons[0]}/complete", null);

        var data = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{lessons[0]}/complete", null))).GetProperty("data");

        Assert.Equal(1, data.GetProperty("completedLessons").GetInt32());
    }

    [Fact]
    public async Task Visiting_a_lesson_moves_the_resume_point_without_completing_it()
    {
        // Opening a lesson and finishing it are different claims. Conflating them would complete a
        // course for someone who merely scrolled through it.
        var (_, _, _, slug) = await SeedPublishedCourseAsync(await SeedBodyAsync("One"));
        var lessons = await PublicLessonIdsAsync(slug);

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);

        var data = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{lessons[0]}/visit", null))).GetProperty("data");

        Assert.Equal(lessons[0], data.GetProperty("lastLessonId").GetGuid());
        Assert.Equal(0, data.GetProperty("completedLessons").GetInt32());
    }

    [Fact]
    public async Task Reopening_a_lesson_un_marks_it()
    {
        var (_, _, _, slug) = await SeedPublishedCourseAsync(
            await SeedBodyAsync("One"), await SeedBodyAsync("Two"));
        var lessons = await PublicLessonIdsAsync(slug);

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}/lessons/{lessons[0]}/complete", null);

        var data = (await ReadAsync(await learner.DeleteAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{lessons[0]}/complete"))).GetProperty("data");

        Assert.Equal(0, data.GetProperty("completedLessons").GetInt32());
    }

    [Fact]
    public async Task Recording_progress_without_enrolling_is_refused()
    {
        var (_, _, _, slug) = await SeedPublishedCourseAsync(await SeedBodyAsync("One"));
        var lessons = await PublicLessonIdsAsync(slug);

        var learner = await LearnerAsync();
        var response = await learner.PostAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{lessons[0]}/complete", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ---- What a learner is allowed to record against ----

    [Fact]
    public async Task A_lesson_whose_body_is_unpublished_cannot_be_completed()
    {
        // Without this the reachable set and the recordable set differ, and a client could tick a
        // lesson the learner cannot open — then complete a course on the strength of it.
        var (_, _, _, slug) = await SeedPublishedCourseAsync(
            await SeedBodyAsync("Ready"), await SeedBodyAsync("Not Ready", publish: false));

        // The public page omits the draft, so its id has to come from the authoring view.
        var editor = await factory.CreateAuthenticatedClientAsync(Roles.Editor);
        var listing = await ReadAsync(await editor.GetAsync("/api/v1/authoring/courses?pageSize=100"));
        var course = listing.GetProperty("data").EnumerateArray()
            .First(c => c.GetProperty("slug").GetString() == slug);
        var authoring = await ReadAsync(await editor.GetAsync(
            $"/api/v1/authoring/courses/{course.GetProperty("id").GetGuid()}"));

        var draftLessonId = authoring.GetProperty("data").GetProperty("modules")[0]
            .GetProperty("lessons").EnumerateArray()
            .First(l => !l.GetProperty("isPublished").GetBoolean())
            .GetProperty("id").GetGuid();

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);

        var response = await learner.PostAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{draftLessonId}/complete", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_lesson_from_a_different_course_cannot_be_completed()
    {
        var (_, _, _, mine) = await SeedPublishedCourseAsync(await SeedBodyAsync("Mine"));
        var (_, _, _, theirs) = await SeedPublishedCourseAsync(await SeedBodyAsync("Theirs"));
        var foreign = (await PublicLessonIdsAsync(theirs))[0];

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{mine}", null);

        var response = await learner.PostAsync(
            $"/api/v1/me/enrollments/{mine}/lessons/{foreign}/complete", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task One_learners_progress_is_invisible_to_another()
    {
        var (_, _, _, slug) = await SeedPublishedCourseAsync(await SeedBodyAsync("One"));
        var lessons = await PublicLessonIdsAsync(slug);

        var alex = await LearnerAsync();
        await alex.PostAsync($"/api/v1/me/enrollments/{slug}", null);
        await alex.PostAsync($"/api/v1/me/enrollments/{slug}/lessons/{lessons[0]}/complete", null);

        var sam = await LearnerAsync();
        Assert.Equal(HttpStatusCode.NotFound,
            (await sam.GetAsync($"/api/v1/me/enrollments/{slug}")).StatusCode);

        await sam.PostAsync($"/api/v1/me/enrollments/{slug}", null);
        var samsProgress = (await ReadAsync(await sam.GetAsync($"/api/v1/me/enrollments/{slug}")))
            .GetProperty("data");

        Assert.Equal(0, samsProgress.GetProperty("completedLessons").GetInt32());
    }

    // ---- Course completion (LN-6) ----

    [Fact]
    public async Task Completing_every_lesson_completes_the_course()
    {
        var (_, _, _, slug) = await SeedPublishedCourseAsync(
            await SeedBodyAsync("One"), await SeedBodyAsync("Two"));
        var lessons = await PublicLessonIdsAsync(slug);

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}/lessons/{lessons[0]}/complete", null);

        var data = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{lessons[1]}/complete", null))).GetProperty("data");

        Assert.NotEqual(JsonValueKind.Null, data.GetProperty("completedAt").ValueKind);
        Assert.Equal(100, data.GetProperty("percentComplete").GetInt32());
    }

    [Fact]
    public async Task A_course_that_grows_after_completion_does_not_un_complete_the_learner()
    {
        // The rule this whole design turns on (LN-6). Derived completion would make publishing a new
        // lesson retroactively revoke every certificate ever issued for the course — not an edge
        // case, but the ordinary consequence of a curriculum growing, which ADR-0013 expects.
        var (editor, courseId, moduleId, slug) = await SeedPublishedCourseAsync(await SeedBodyAsync("One"));
        var lessons = await PublicLessonIdsAsync(slug);

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);
        var finished = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{lessons[0]}/complete", null))).GetProperty("data");

        var completedAt = finished.GetProperty("completedAt").GetDateTimeOffset();

        // The course grows.
        (await editor.PostAsJsonAsync(
            $"/api/v1/authoring/courses/{courseId}/modules/{moduleId}/lessons",
            new { contentUnitId = await SeedBodyAsync("Two") })).EnsureSuccessStatusCode();

        var after = (await ReadAsync(await learner.GetAsync($"/api/v1/me/enrollments/{slug}")))
            .GetProperty("data");

        // Still complete, at the same moment it always was. Compared with a tolerance because
        // PostgreSQL `timestamptz` is microsecond-precision and a .NET tick is 100ns, so a value
        // that has been through the database is never bit-identical to the one that went in.
        Assert.Equal(
            completedAt,
            after.GetProperty("completedAt").GetDateTimeOffset(),
            TimeSpan.FromMilliseconds(1));

        // And honest about the arithmetic: one of two done, but the course is finished.
        Assert.Equal(1, after.GetProperty("completedLessons").GetInt32());
        Assert.Equal(2, after.GetProperty("totalLessons").GetInt32());
        Assert.Equal(50, after.GetProperty("percentComplete").GetInt32());
    }

    [Fact]
    public async Task Reopening_a_lesson_does_not_revoke_a_completed_course()
    {
        // Same rule from the other direction: completion is a moment, and un-ticking a row is not a
        // time machine.
        var (_, _, _, slug) = await SeedPublishedCourseAsync(await SeedBodyAsync("One"));
        var lessons = await PublicLessonIdsAsync(slug);

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}/lessons/{lessons[0]}/complete", null);

        var data = (await ReadAsync(await learner.DeleteAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{lessons[0]}/complete"))).GetProperty("data");

        Assert.NotEqual(JsonValueKind.Null, data.GetProperty("completedAt").ValueKind);
        Assert.Equal(0, data.GetProperty("completedLessons").GetInt32());
    }

    // ---- The quiz gate (AS-9 / D-1) ----

    /// <summary>
    /// A one-question single-choice quiz bound to a curriculum lesson, published by default. Returns
    /// the question and its correct/wrong choice so a test can pass or fail it.
    /// </summary>
    private static async Task<(Guid QuestionId, Guid Correct, Guid Wrong)> SeedQuizForLessonAsync(
        HttpClient editor, Guid lessonId, bool publish = true)
    {
        var created = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/quizzes",
            new { lessonId, title = "Lesson check", passingScore = 50 }));
        var quizId = created.GetProperty("data").GetProperty("id").GetGuid();

        var withQuestion = await ReadAsync(await editor.PostAsJsonAsync(
            $"/api/v1/authoring/quizzes/{quizId}/questions",
            new { prompt = "Understood?", type = "singlechoice", points = 1 }));
        var questionId = withQuestion.GetProperty("data").GetProperty("questions")[0]
            .GetProperty("id").GetGuid();

        foreach (var text in new[] { "Yes", "No" })
            (await editor.PostAsJsonAsync(
                $"/api/v1/authoring/quizzes/{quizId}/questions/{questionId}/choices", new { text }))
                .EnsureSuccessStatusCode();

        var withChoices = await ReadAsync(await editor.GetAsync($"/api/v1/authoring/quizzes/{quizId}"));
        var choices = withChoices.GetProperty("data").GetProperty("questions")[0]
            .GetProperty("choices").EnumerateArray().ToList();
        var correct = choices.First(c => c.GetProperty("text").GetString() == "Yes").GetProperty("id").GetGuid();
        var wrong = choices.First(c => c.GetProperty("text").GetString() == "No").GetProperty("id").GetGuid();

        (await editor.PutAsJsonAsync(
            $"/api/v1/authoring/quizzes/{quizId}/questions/{questionId}/answer",
            new { correctChoiceIds = new[] { correct } })).EnsureSuccessStatusCode();

        if (publish)
            (await editor.PostAsync($"/api/v1/authoring/quizzes/{quizId}/publish", null))
                .EnsureSuccessStatusCode();

        return (questionId, correct, wrong);
    }

    private static async Task SubmitAttemptAsync(
        HttpClient learner, Guid lessonId, Guid questionId, Guid choiceId)
    {
        var attemptId = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/lessons/{lessonId}/quiz/attempts", null))).GetProperty("data").GetProperty("id").GetGuid();

        (await learner.PostAsJsonAsync($"/api/v1/me/attempts/{attemptId}/submit",
            new { answers = new Dictionary<Guid, Guid[]> { [questionId] = [choiceId] } }))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_lesson_with_a_published_quiz_cannot_be_completed_until_it_is_passed()
    {
        var (editor, _, _, slug) = await SeedPublishedCourseAsync(await SeedBodyAsync("One"));
        var lessonId = (await PublicLessonIdsAsync(slug))[0];
        await SeedQuizForLessonAsync(editor, lessonId);

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);

        var blocked = await learner.PostAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{lessonId}/complete", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
    }

    [Fact]
    public async Task Passing_the_quiz_unlocks_completion()
    {
        var (editor, _, _, slug) = await SeedPublishedCourseAsync(await SeedBodyAsync("One"));
        var lessonId = (await PublicLessonIdsAsync(slug))[0];
        var (questionId, correct, _) = await SeedQuizForLessonAsync(editor, lessonId);

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);
        await SubmitAttemptAsync(learner, lessonId, questionId, correct);

        var data = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{lessonId}/complete", null))).GetProperty("data");

        Assert.Equal(1, data.GetProperty("completedLessons").GetInt32());
    }

    [Fact]
    public async Task A_failed_attempt_does_not_unlock_completion()
    {
        // A submitted attempt exists, but the gate wants a passing one — submitting and failing is not
        // a way around it.
        var (editor, _, _, slug) = await SeedPublishedCourseAsync(await SeedBodyAsync("One"));
        var lessonId = (await PublicLessonIdsAsync(slug))[0];
        var (questionId, _, wrong) = await SeedQuizForLessonAsync(editor, lessonId);

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);
        await SubmitAttemptAsync(learner, lessonId, questionId, wrong);

        var blocked = await learner.PostAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{lessonId}/complete", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
    }

    [Fact]
    public async Task A_draft_quiz_does_not_gate_completion()
    {
        // Only a published quiz is a promise to the learner. A draft one must not lock a lesson that
        // was completable the moment before the author started writing it.
        var (editor, _, _, slug) = await SeedPublishedCourseAsync(await SeedBodyAsync("One"));
        var lessonId = (await PublicLessonIdsAsync(slug))[0];
        await SeedQuizForLessonAsync(editor, lessonId, publish: false);

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);

        var data = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{lessonId}/complete", null))).GetProperty("data");

        Assert.Equal(1, data.GetProperty("completedLessons").GetInt32());
    }

    [Fact]
    public async Task A_quiz_added_after_completion_does_not_revoke_it()
    {
        // The gate stands in front of a completion still to be made, never behind one already made —
        // the same one-way stance LN-6 takes on a growing curriculum.
        var (editor, _, _, slug) = await SeedPublishedCourseAsync(await SeedBodyAsync("One"));
        var lessonId = (await PublicLessonIdsAsync(slug))[0];

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{slug}", null);
        (await learner.PostAsync($"/api/v1/me/enrollments/{slug}/lessons/{lessonId}/complete", null))
            .EnsureSuccessStatusCode();

        // The author gates the lesson only now, after the learner already finished it.
        await SeedQuizForLessonAsync(editor, lessonId);

        var after = (await ReadAsync(await learner.GetAsync($"/api/v1/me/enrollments/{slug}")))
            .GetProperty("data");
        Assert.Equal(1, after.GetProperty("completedLessons").GetInt32());

        // And re-completing it stays the no-op it always was, rather than turning into a refusal.
        var again = await learner.PostAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{lessonId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    // ---- The dashboard ----

    [Fact]
    public async Task The_dashboard_lists_the_learners_courses_most_recent_first()
    {
        var (_, _, _, older) = await SeedPublishedCourseAsync(await SeedBodyAsync("Older"));
        var (_, _, _, newer) = await SeedPublishedCourseAsync(await SeedBodyAsync("Newer"));

        var learner = await LearnerAsync();
        await learner.PostAsync($"/api/v1/me/enrollments/{older}", null);
        await learner.PostAsync($"/api/v1/me/enrollments/{newer}", null);

        // Touching the older one moves it to the top: the dashboard orders by activity, not by
        // when the learner signed up.
        var olderLesson = (await PublicLessonIdsAsync(older))[0];
        await learner.PostAsync($"/api/v1/me/enrollments/{older}/lessons/{olderLesson}/visit", null);

        var listing = await ReadAsync(await learner.GetAsync("/api/v1/me/enrollments"));
        var slugs = listing.GetProperty("data").EnumerateArray()
            .Select(e => e.GetProperty("courseSlug").GetString())
            .ToArray();

        Assert.Equal(older, slugs[0]);
        Assert.Equal(newer, slugs[1]);
    }

    [Fact]
    public async Task The_resume_point_carries_a_slug_so_a_client_can_link_to_it()
    {
        // The id alone cannot be turned into a URL, and a dashboard that has to guess would guess
        // wrong. Null rather than a stale slug is the useful answer when the lesson is unreachable.
        var (_, _, _, slug) = await SeedPublishedCourseAsync(await SeedBodyAsync("Resumable"));
        var lessons = await PublicLessonIdsAsync(slug);

        var learner = await LearnerAsync();
        var fresh = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/me/enrollments/{slug}", null))).GetProperty("data");

        // Nothing opened yet, so there is nothing to resume.
        Assert.Equal(JsonValueKind.Null, fresh.GetProperty("lastLessonSlug").ValueKind);

        var visited = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/me/enrollments/{slug}/lessons/{lessons[0]}/visit", null))).GetProperty("data");

        Assert.False(string.IsNullOrWhiteSpace(visited.GetProperty("lastLessonSlug").GetString()));
        Assert.Equal(lessons[0], visited.GetProperty("lastLessonId").GetGuid());
    }
}
