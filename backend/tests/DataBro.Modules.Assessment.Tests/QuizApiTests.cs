using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataBro.Modules.Identity.Domain;
using Xunit;

namespace DataBro.Modules.Assessment.Tests;

/// <summary>
/// Quizzes end to end.
///
/// The test that matters most is the one asserting a learner never receives the answer key. That
/// failure is silent — a quiz that ships its own answers looks and works exactly like one that does
/// not — so it has to be caught here or not at all.
/// </summary>
public class QuizApiTests(AssessmentApiFactory factory) : IClassFixture<AssessmentApiFactory>
{
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private Task<HttpClient> EditorAsync() => factory.CreateAuthenticatedClientAsync(Roles.Editor);
    private Task<HttpClient> LearnerAsync() => factory.CreateAuthenticatedClientAsync(Roles.Reader);

    /// <summary>
    /// A published quiz on a fresh lesson id: two single-choice questions, one point each.
    /// Returns the editor, the quiz id, the lesson id, and the correct choice per question.
    /// </summary>
    private async Task<(HttpClient Editor, Guid QuizId, Guid LessonId, List<(Guid QuestionId, Guid CorrectChoiceId, Guid WrongChoiceId)> Key)>
        SeedQuizAsync(bool publish = true)
    {
        var editor = await EditorAsync();

        // A bare id: Assessment references lessons across a module boundary and never validates
        // that one exists, exactly as Learning does for content bodies.
        var lessonId = Guid.NewGuid();

        var created = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/quizzes",
            new { lessonId, title = "Retrieval Basics", passingScore = 50 }));
        var quizId = created.GetProperty("data").GetProperty("id").GetGuid();

        var key = new List<(Guid, Guid, Guid)>();

        foreach (var prompt in new[] { "What is chunking?", "What is an embedding?" })
        {
            var withQuestion = await ReadAsync(await editor.PostAsJsonAsync(
                $"/api/v1/authoring/quizzes/{quizId}/questions",
                new { prompt, type = "singlechoice", points = 1 }));

            var questionId = withQuestion.GetProperty("data").GetProperty("questions")
                .EnumerateArray().First(q => q.GetProperty("prompt").GetString() == prompt)
                .GetProperty("id").GetGuid();

            foreach (var text in new[] { "The right answer", "A wrong answer" })
            {
                (await editor.PostAsJsonAsync(
                    $"/api/v1/authoring/quizzes/{quizId}/questions/{questionId}/choices",
                    new { text })).EnsureSuccessStatusCode();
            }

            var withChoices = await ReadAsync(await editor.GetAsync($"/api/v1/authoring/quizzes/{quizId}"));
            var choices = withChoices.GetProperty("data").GetProperty("questions")
                .EnumerateArray().First(q => q.GetProperty("id").GetGuid() == questionId)
                .GetProperty("choices").EnumerateArray().ToList();

            var correct = choices.First(c => c.GetProperty("text").GetString() == "The right answer")
                .GetProperty("id").GetGuid();
            var wrong = choices.First(c => c.GetProperty("text").GetString() == "A wrong answer")
                .GetProperty("id").GetGuid();

            (await editor.PutAsJsonAsync(
                $"/api/v1/authoring/quizzes/{quizId}/questions/{questionId}/answer",
                new { correctChoiceIds = new[] { correct } })).EnsureSuccessStatusCode();

            key.Add((questionId, correct, wrong));
        }

        if (publish)
            (await editor.PostAsync($"/api/v1/authoring/quizzes/{quizId}/publish", null))
                .EnsureSuccessStatusCode();

        return (editor, quizId, lessonId, key);
    }

    // ---- The rule this module exists to keep ----

    [Fact]
    public async Task A_learner_never_receives_the_answer_key()
    {
        // Asserted on the raw JSON, not the parsed shape: the point is that the bytes reaching the
        // browser contain nothing about correctness, however the DTO happens to be structured.
        var (_, _, lessonId, _) = await SeedQuizAsync();
        var learner = await LearnerAsync();

        var response = await learner.GetAsync($"/api/v1/lessons/{lessonId}/quiz");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("What is chunking?", raw);
        Assert.DoesNotContain("isCorrect", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correctChoiceIds", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_in_progress_attempt_carries_no_results()
    {
        // The other half of the same rule: starting an attempt must not hand over the answers, and
        // an unsubmitted attempt has nothing to review.
        var (_, _, lessonId, _) = await SeedQuizAsync();
        var learner = await LearnerAsync();

        var started = await learner.PostAsync($"/api/v1/lessons/{lessonId}/quiz/attempts", null);
        var raw = await started.Content.ReadAsStringAsync();
        var data = (await ReadAsync(started)).GetProperty("data");

        Assert.Empty(data.GetProperty("results").EnumerateArray());
        Assert.DoesNotContain("correctChoiceIds", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_answer_key_arrives_only_after_submission()
    {
        var (_, _, lessonId, key) = await SeedQuizAsync();
        var learner = await LearnerAsync();

        var attemptId = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/lessons/{lessonId}/quiz/attempts", null)))
            .GetProperty("data").GetProperty("id").GetGuid();

        var submitted = await ReadAsync(await learner.PostAsJsonAsync(
            $"/api/v1/me/attempts/{attemptId}/submit",
            new { answers = key.ToDictionary(k => k.QuestionId, k => new[] { k.CorrectChoiceId }) }));

        var results = submitted.GetProperty("data").GetProperty("results").EnumerateArray().ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.NotEmpty(r.GetProperty("correctChoiceIds").EnumerateArray()));
    }

    // ---- Scoring ----

    [Fact]
    public async Task All_correct_passes_with_full_marks()
    {
        var (_, _, lessonId, key) = await SeedQuizAsync();
        var learner = await LearnerAsync();

        var attemptId = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/lessons/{lessonId}/quiz/attempts", null)))
            .GetProperty("data").GetProperty("id").GetGuid();

        var data = (await ReadAsync(await learner.PostAsJsonAsync(
            $"/api/v1/me/attempts/{attemptId}/submit",
            new { answers = key.ToDictionary(k => k.QuestionId, k => new[] { k.CorrectChoiceId }) })))
            .GetProperty("data");

        Assert.Equal(2, data.GetProperty("score").GetInt32());
        Assert.Equal(2, data.GetProperty("totalPoints").GetInt32());
        Assert.Equal(100, data.GetProperty("percentage").GetInt32());
        Assert.True(data.GetProperty("passed").GetBoolean());
    }

    [Fact]
    public async Task A_client_cannot_submit_its_own_score()
    {
        // Scoring happens in the domain from the stored key. The request shape carries selections
        // only, so a fabricated score has nowhere to go — this pins that a wrong answer stays wrong
        // no matter what else is in the body.
        var (_, _, lessonId, key) = await SeedQuizAsync();
        var learner = await LearnerAsync();

        var attemptId = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/lessons/{lessonId}/quiz/attempts", null)))
            .GetProperty("data").GetProperty("id").GetGuid();

        var data = (await ReadAsync(await learner.PostAsJsonAsync(
            $"/api/v1/me/attempts/{attemptId}/submit",
            new
            {
                answers = key.ToDictionary(k => k.QuestionId, k => new[] { k.WrongChoiceId }),
                score = 999,
                passed = true,
            })))
            .GetProperty("data");

        Assert.Equal(0, data.GetProperty("score").GetInt32());
        Assert.False(data.GetProperty("passed").GetBoolean());
    }

    [Fact]
    public async Task An_unanswered_question_scores_zero_rather_than_failing_the_submission()
    {
        var (_, _, lessonId, key) = await SeedQuizAsync();
        var learner = await LearnerAsync();

        var attemptId = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/lessons/{lessonId}/quiz/attempts", null)))
            .GetProperty("data").GetProperty("id").GetGuid();

        // Only the first question answered.
        var data = (await ReadAsync(await learner.PostAsJsonAsync(
            $"/api/v1/me/attempts/{attemptId}/submit",
            new { answers = new Dictionary<Guid, Guid[]> { [key[0].QuestionId] = [key[0].CorrectChoiceId] } })))
            .GetProperty("data");

        Assert.Equal(1, data.GetProperty("score").GetInt32());
        Assert.Equal(50, data.GetProperty("percentage").GetInt32());
        // Recorded as answered-with-nothing, not omitted: "left blank" and "not shown" are different
        // facts and only one is true.
        Assert.Equal(2, data.GetProperty("results").GetArrayLength());
    }

    // ---- Attempts ----

    [Fact]
    public async Task Starting_twice_resumes_rather_than_discarding_the_first_attempt()
    {
        // A page reload is not a decision to throw away answers.
        var (_, _, lessonId, _) = await SeedQuizAsync();
        var learner = await LearnerAsync();

        var first = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/lessons/{lessonId}/quiz/attempts", null))).GetProperty("data").GetProperty("id").GetGuid();
        var second = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/lessons/{lessonId}/quiz/attempts", null))).GetProperty("data").GetProperty("id").GetGuid();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task An_attempt_cannot_be_submitted_twice()
    {
        var (_, _, lessonId, key) = await SeedQuizAsync();
        var learner = await LearnerAsync();

        var attemptId = (await ReadAsync(await learner.PostAsync(
            $"/api/v1/lessons/{lessonId}/quiz/attempts", null)))
            .GetProperty("data").GetProperty("id").GetGuid();

        var body = new { answers = key.ToDictionary(k => k.QuestionId, k => new[] { k.CorrectChoiceId }) };

        (await learner.PostAsJsonAsync($"/api/v1/me/attempts/{attemptId}/submit", body))
            .EnsureSuccessStatusCode();

        var again = await learner.PostAsJsonAsync($"/api/v1/me/attempts/{attemptId}/submit", body);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task One_learners_attempt_is_invisible_to_another()
    {
        var (_, _, lessonId, _) = await SeedQuizAsync();

        var alex = await LearnerAsync();
        var attemptId = (await ReadAsync(await alex.PostAsync(
            $"/api/v1/lessons/{lessonId}/quiz/attempts", null)))
            .GetProperty("data").GetProperty("id").GetGuid();

        var sam = await LearnerAsync();

        // A missing attempt and someone else's are the same answer, so an id cannot be probed.
        Assert.Equal(HttpStatusCode.NotFound,
            (await sam.GetAsync($"/api/v1/me/attempts/{attemptId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await sam.PostAsJsonAsync($"/api/v1/me/attempts/{attemptId}/submit",
                new { answers = new Dictionary<Guid, Guid[]>() })).StatusCode);
    }

    // ---- Publishing ----

    [Fact]
    public async Task An_unpublished_quiz_is_not_visible_to_a_learner()
    {
        var (_, _, lessonId, _) = await SeedQuizAsync(publish: false);
        var learner = await LearnerAsync();

        Assert.Equal(HttpStatusCode.NotFound,
            (await learner.GetAsync($"/api/v1/lessons/{lessonId}/quiz")).StatusCode);
    }

    [Fact]
    public async Task A_question_with_no_correct_answer_blocks_publishing()
    {
        // Publishing it would ship a question nobody can answer correctly — a trap rather than an
        // incomplete offering, which is why this is stricter than a course's publish rule.
        var editor = await EditorAsync();

        var created = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/quizzes",
            new { lessonId = Guid.NewGuid(), title = "Broken", passingScore = 50 }));
        var quizId = created.GetProperty("data").GetProperty("id").GetGuid();

        var withQuestion = await ReadAsync(await editor.PostAsJsonAsync(
            $"/api/v1/authoring/quizzes/{quizId}/questions",
            new { prompt = "Unanswerable", type = "singlechoice", points = 1 }));
        var questionId = withQuestion.GetProperty("data").GetProperty("questions")[0]
            .GetProperty("id").GetGuid();

        foreach (var text in new[] { "A", "B" })
        {
            (await editor.PostAsJsonAsync(
                $"/api/v1/authoring/quizzes/{quizId}/questions/{questionId}/choices",
                new { text })).EnsureSuccessStatusCode();
        }

        var response = await editor.PostAsync($"/api/v1/authoring/quizzes/{quizId}/publish", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task A_single_choice_question_refuses_two_correct_answers()
    {
        var (editor, quizId, _, key) = await SeedQuizAsync();

        var response = await editor.PutAsJsonAsync(
            $"/api/v1/authoring/quizzes/{quizId}/questions/{key[0].QuestionId}/answer",
            new { correctChoiceIds = new[] { key[0].CorrectChoiceId, key[0].WrongChoiceId } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_lesson_can_only_have_one_quiz()
    {
        var (editor, _, lessonId, _) = await SeedQuizAsync();

        var response = await editor.PostAsJsonAsync("/api/v1/authoring/quizzes",
            new { lessonId, title = "A second one", passingScore = 50 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Authoring_a_quiz_requires_an_editorial_permission()
    {
        var learner = await LearnerAsync();

        var response = await learner.PostAsJsonAsync("/api/v1/authoring/quizzes",
            new { lessonId = Guid.NewGuid(), title = "Nope", passingScore = 50 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
