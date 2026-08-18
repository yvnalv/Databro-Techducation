using DataBro.Modules.Assessment.Domain;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Results;

namespace DataBro.Modules.Assessment.Application;

/// <summary>
/// A learner's attempts.
///
/// <para>
/// Takes the learner's id explicitly rather than reading an ambient <c>ICurrentUser</c>, for the
/// reason <c>EnrollmentService</c> does: here the id <i>is</i> the authorization boundary, and an
/// implicit parameter is the kind that gets forgotten in one branch and scores someone else's paper.
/// </para>
/// </summary>
public sealed class AttemptService(
    IQuizAttemptRepository attempts,
    IQuizRepository quizzes,
    IClock clock)
{
    /// <summary>
    /// Starts an attempt, or resumes the learner's open one.
    ///
    /// <para>
    /// Resuming rather than starting afresh because a page reload is not a decision to throw away
    /// answers. A genuinely new run happens once the previous attempt has been submitted.
    /// </para>
    /// </summary>
    public async Task<Result<AttemptDto>> StartAsync(
        Guid userId, Guid lessonId, CancellationToken ct = default)
    {
        var quiz = await quizzes.GetPublishedForLessonAsync(lessonId, ct);
        if (quiz is null)
            return Result.Failure<AttemptDto>(Error.NotFound("No published quiz for that lesson."));

        var open = await attempts.GetOpenAttemptAsync(userId, quiz.Id, ct);
        if (open is not null) return Result.Success(Compose(open, quiz));

        var attempt = QuizAttempt.Start(Guid.NewGuid(), quiz.Id, userId, clock.UtcNow);

        await attempts.AddAsync(attempt, ct);
        await attempts.SaveChangesAsync(ct);

        return Result.Success(Compose(attempt, quiz));
    }

    /// <summary>
    /// Submits and scores an attempt.
    ///
    /// <para>
    /// The scoring is done by the aggregate from the stored answer key. Nothing a client sends
    /// influences the score — the request carries selections only.
    /// </para>
    /// </summary>
    public async Task<Result<AttemptDto>> SubmitAsync(
        Guid userId, Guid attemptId, SubmitAttemptRequest request, CancellationToken ct = default)
    {
        var attempt = await attempts.GetAsync(attemptId, ct);

        // A missing attempt and someone else's are the same answer. Distinguishing them would let
        // anyone probe which attempt ids exist.
        if (attempt is null || attempt.UserId != userId)
            return Result.Failure<AttemptDto>(Error.NotFound("Attempt not found."));

        var quiz = await quizzes.GetByIdAsync(attempt.QuizId, ct);
        if (quiz is null)
            return Result.Failure<AttemptDto>(Error.NotFound("Quiz not found."));

        var selections = request.Answers.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyCollection<Guid>)kvp.Value);

        var result = attempt.Submit(quiz, selections, clock.UtcNow);
        if (result.IsFailure) return Result.Failure<AttemptDto>(result.Error);

        await attempts.SaveChangesAsync(ct);

        return Result.Success(Compose(attempt, quiz));
    }

    /// <summary>One attempt, if it belongs to this learner.</summary>
    public async Task<AttemptDto?> GetAsync(Guid userId, Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await attempts.GetAsync(attemptId, ct);
        if (attempt is null || attempt.UserId != userId) return null;

        var quiz = await quizzes.GetByIdAsync(attempt.QuizId, ct);
        return quiz is null ? null : Compose(attempt, quiz);
    }

    /// <summary>The learner's history for a lesson's quiz, newest first.</summary>
    public async Task<IReadOnlyList<AttemptDto>> ListForLessonAsync(
        Guid userId, Guid lessonId, CancellationToken ct = default)
    {
        var quiz = await quizzes.GetForLessonAsync(lessonId, ct);
        if (quiz is null) return [];

        var history = await attempts.ListForLearnerAsync(userId, quiz.Id, ct);
        return history.Select(a => Compose(a, quiz)).ToList();
    }

    /// <summary>
    /// Projects an attempt.
    ///
    /// <para>
    /// <b>Results — including the answer key — are attached only once the attempt is submitted.</b>
    /// Before that the attempt is in progress and returning correct choices would simply be handing
    /// over the answers. After it, the attempt cannot be changed, so the same data is feedback.
    /// That single condition is the whole rule, which is why it lives in one place.
    /// </para>
    /// </summary>
    private static AttemptDto Compose(QuizAttempt attempt, Quiz quiz)
    {
        var results = attempt.IsSubmitted
            ? attempt.Answers
                .Select(answer =>
                {
                    var question = quiz.FindQuestion(answer.QuestionId);

                    return new AttemptAnswerResultDto(
                        answer.QuestionId,
                        answer.SelectedChoiceIds,
                        question is null
                            ? []
                            : question.Choices.Where(c => c.IsCorrect).Select(c => c.Id).ToList(),
                        answer.PointsEarned,
                        question?.Explanation);
                })
                .ToList()
            : [];

        return new AttemptDto(
            attempt.Id,
            attempt.QuizId,
            attempt.StartedAt,
            attempt.SubmittedAt,
            attempt.Score,
            attempt.IsSubmitted ? attempt.TotalPoints : quiz.TotalPoints,
            attempt.Percentage,
            attempt.Passed,
            results);
    }
}
