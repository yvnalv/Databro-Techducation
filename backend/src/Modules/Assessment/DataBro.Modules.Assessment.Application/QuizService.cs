using DataBro.Modules.Assessment.Domain;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Results;

namespace DataBro.Modules.Assessment.Application;

/// <summary>Authoring use cases for the <see cref="Quiz"/> aggregate.</summary>
public sealed class QuizService(
    IQuizRepository quizzes,
    IQuizAttemptRepository attempts,
    IUserDirectory users,
    IClock clock)
{
    // ---- Reads ----

    /// <summary>
    /// The learner's view of a lesson's quiz. Published only, and projected through
    /// <see cref="AssessmentMapping.ToLearnerDto"/>, which cannot carry the answer key.
    /// </summary>
    public async Task<QuizDto?> GetForLessonAsync(Guid lessonId, CancellationToken ct = default)
    {
        var quiz = await quizzes.GetPublishedForLessonAsync(lessonId, ct);
        return quiz?.ToLearnerDto();
    }

    public async Task<AuthoringQuizDto?> GetForAuthoringAsync(Guid id, CancellationToken ct = default)
    {
        var quiz = await quizzes.GetByIdAsync(id, ct);
        return quiz?.ToAuthoringDto();
    }

    /// <summary>
    /// The authoring view of a lesson's quiz, whatever its status. Lets a builder answer "does this
    /// lesson have one yet" in a single request rather than one per lesson on load.
    /// </summary>
    public async Task<AuthoringQuizDto?> GetForAuthoringByLessonAsync(
        Guid lessonId, CancellationToken ct = default)
    {
        var quiz = await quizzes.GetForLessonAsync(lessonId, ct);
        return quiz?.ToAuthoringDto();
    }

    public async Task<PagedResult<AuthoringQuizDto>> ListAllAsync(
        PageRequest page, CancellationToken ct = default)
    {
        var result = await quizzes.ListAllAsync(page, ct);

        return new PagedResult<AuthoringQuizDto>(
            result.Items.Select(q => q.ToAuthoringDto()).ToList(),
            result.Page, result.PageSize, result.Total);
    }

    /// <summary>
    /// The submitted attempts at a quiz, for the author's review screen (U-1). Null when the quiz
    /// does not exist, so the endpoint can 404; an empty list means it exists but nobody has taken it.
    /// </summary>
    public async Task<IReadOnlyList<AttemptSummaryDto>?> ListAttemptsAsync(
        Guid quizId, CancellationToken ct = default)
    {
        // Confirm the quiz exists before reporting "no attempts": an unknown id and a quiz nobody has
        // taken are different answers, and only the second is an empty list.
        if (await quizzes.GetByIdAsync(quizId, ct) is null) return null;

        var taken = await attempts.ListForQuizAsync(quizId, ct);
        if (taken.Count == 0) return [];

        // One batched lookup for every learner on the screen — the reason IUserDirectory is
        // batch-shaped (ADR-0008) rather than resolved one row at a time.
        var directory = await users.GetUsersAsync(taken.Select(a => a.UserId).Distinct().ToList(), ct);

        return taken
            .Select(a => new AttemptSummaryDto(
                a.Id,
                a.UserId,
                // A learner whose account was since deleted still has attempts worth showing; the
                // directory tolerates a partial map, so a missing name degrades rather than breaks.
                directory.TryGetValue(a.UserId, out var user) ? user.DisplayName : "Unknown learner",
                a.Score,
                a.TotalPoints,
                a.Percentage,
                a.Passed,
                a.SubmittedAt!.Value))
            .ToList();
    }

    // ---- Authoring ----

    public async Task<Result<AuthoringQuizDto>> CreateAsync(
        CreateQuizRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result.Failure<AuthoringQuizDto>(Error.Validation("Title is required."));

        // One quiz per lesson. Not a database constraint but a rule with a reason: "the quiz for
        // this lesson" is how every other surface refers to it, and two would make that phrase
        // meaningless.
        if (await quizzes.GetForLessonAsync(request.LessonId, ct) is not null)
            return Result.Failure<AuthoringQuizDto>(
                Error.Conflict("That lesson already has a quiz."));

        var quiz = Quiz.CreateDraft(Guid.NewGuid(), request.LessonId, request.Title, request.PassingScore);

        await quizzes.AddAsync(quiz, ct);
        await quizzes.SaveChangesAsync(ct);

        return Result.Success(quiz.ToAuthoringDto());
    }

    public Task<Result<AuthoringQuizDto>> UpdateAsync(
        Guid id, UpdateQuizRequest request, CancellationToken ct = default)
        => MutateAsync(id, quiz =>
        {
            quiz.Describe(request.Title, request.PassingScore);
            return Result.Success();
        }, ct);

    public Task<Result<AuthoringQuizDto>> AddQuestionAsync(
        Guid id, AddQuestionRequest request, CancellationToken ct = default)
        => MutateAsync(id, quiz =>
        {
            quiz.AddQuestion(Guid.NewGuid(), request.Prompt,
                AssessmentMapping.ParseQuestionType(request.Type), request.Points);
            return Result.Success();
        }, ct);

    public Task<Result<AuthoringQuizDto>> UpdateQuestionAsync(
        Guid id, Guid questionId, UpdateQuestionRequest request, CancellationToken ct = default)
        => MutateAsync(id, quiz =>
        {
            var question = quiz.FindQuestion(questionId);
            if (question is null) return Result.Failure(Error.NotFound("Question not found in this quiz."));

            question.Describe(request.Prompt, request.Points, request.Explanation);
            return Result.Success();
        }, ct);

    public Task<Result<AuthoringQuizDto>> RemoveQuestionAsync(
        Guid id, Guid questionId, CancellationToken ct = default)
        => MutateAsync(id, quiz => quiz.RemoveQuestion(questionId), ct);

    public Task<Result<AuthoringQuizDto>> ReorderQuestionsAsync(
        Guid id, ReorderRequest request, CancellationToken ct = default)
        => MutateAsync(id, quiz => quiz.ReorderQuestions(request.OrderedIds), ct);

    public Task<Result<AuthoringQuizDto>> AddChoiceAsync(
        Guid id, Guid questionId, AddChoiceRequest request, CancellationToken ct = default)
        => MutateAsync(id, quiz =>
        {
            var question = quiz.FindQuestion(questionId);
            if (question is null) return Result.Failure(Error.NotFound("Question not found in this quiz."));

            return question.AddChoice(Guid.NewGuid(), request.Text, isCorrect: false);
        }, ct);

    public Task<Result<AuthoringQuizDto>> RemoveChoiceAsync(
        Guid id, Guid questionId, Guid choiceId, CancellationToken ct = default)
        => MutateAsync(id, quiz =>
        {
            var question = quiz.FindQuestion(questionId);
            if (question is null) return Result.Failure(Error.NotFound("Question not found in this quiz."));

            return question.RemoveChoice(choiceId);
        }, ct);

    /// <summary>Replaces a question's answer key. Set as a whole — see <see cref="Question.SetCorrectChoices"/>.</summary>
    public Task<Result<AuthoringQuizDto>> SetCorrectChoicesAsync(
        Guid id, Guid questionId, SetCorrectChoicesRequest request, CancellationToken ct = default)
        => MutateAsync(id, quiz =>
        {
            var question = quiz.FindQuestion(questionId);
            if (question is null) return Result.Failure(Error.NotFound("Question not found in this quiz."));

            return question.SetCorrectChoices(request.CorrectChoiceIds);
        }, ct);

    public Task<Result<AuthoringQuizDto>> PublishAsync(Guid id, CancellationToken ct = default)
        => MutateAsync(id, quiz => quiz.Publish(clock.UtcNow), ct);

    public Task<Result<AuthoringQuizDto>> UnpublishAsync(Guid id, CancellationToken ct = default)
        => MutateAsync(id, quiz => quiz.Unpublish(), ct);

    private async Task<Result<AuthoringQuizDto>> MutateAsync(
        Guid id, Func<Quiz, Result> mutate, CancellationToken ct)
    {
        var quiz = await quizzes.GetByIdAsync(id, ct);
        if (quiz is null) return Result.Failure<AuthoringQuizDto>(Error.NotFound("Quiz not found."));

        var result = mutate(quiz);
        if (result.IsFailure) return Result.Failure<AuthoringQuizDto>(result.Error);

        await quizzes.SaveChangesAsync(ct);

        // The whole quiz comes back from every mutation, so a builder UI never reconciles a patch —
        // the same contract the course and path builders rely on.
        return Result.Success(quiz.ToAuthoringDto());
    }
}
