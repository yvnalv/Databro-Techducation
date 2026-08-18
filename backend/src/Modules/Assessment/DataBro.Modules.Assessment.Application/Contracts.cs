using DataBro.Modules.Assessment.Domain;

namespace DataBro.Modules.Assessment.Application;

// DTOs exchanged with the API layer. Enums cross the wire lowercase, matching the other modules.
//
// The split between the learner shapes and the authoring shapes is this module's single most
// important rule, so the two sets are kept apart here rather than sharing a type with a nullable
// field. A shared type with `IsCorrect?` would put the answer key one forgotten null-check away from
// the public path, and that failure is silent — a quiz that ships its answers still works perfectly.

// ---- What a learner may see ----

/// <summary>A choice, as the person answering sees it. <b>There is no correctness here, by type.</b></summary>
public sealed record ChoiceDto(Guid Id, string Text);

public sealed record QuestionDto(
    Guid Id,
    string Prompt,
    string Type,
    int Points,
    IReadOnlyList<ChoiceDto> Choices);

public sealed record QuizDto(
    Guid Id,
    Guid LessonId,
    string Title,
    int PassingScore,
    int TotalPoints,
    IReadOnlyList<QuestionDto> Questions);

// ---- What an author may see ----

/// <summary>The authoring view of a choice. Carries the answer key; never reachable by a learner.</summary>
public sealed record AuthoringChoiceDto(Guid Id, string Text, bool IsCorrect);

public sealed record AuthoringQuestionDto(
    Guid Id,
    string Prompt,
    string Type,
    int Points,
    string? Explanation,
    IReadOnlyList<AuthoringChoiceDto> Choices);

public sealed record AuthoringQuizDto(
    Guid Id,
    Guid LessonId,
    string Title,
    string Status,
    int PassingScore,
    int TotalPoints,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<AuthoringQuestionDto> Questions);

// ---- Attempts ----

/// <summary>
/// One question's outcome, returned only <b>after</b> submission.
/// </summary>
/// <param name="CorrectChoiceIds">
/// The answer key, released at exactly one moment: once the attempt is closed and can no longer be
/// changed. Before that it would be the answers; after it, it is feedback.
/// </param>
public sealed record AttemptAnswerResultDto(
    Guid QuestionId,
    IReadOnlyList<Guid> SelectedChoiceIds,
    IReadOnlyList<Guid> CorrectChoiceIds,
    int PointsEarned,
    string? Explanation);

public sealed record AttemptDto(
    Guid Id,
    Guid QuizId,
    DateTimeOffset StartedAt,
    DateTimeOffset? SubmittedAt,
    int Score,
    int TotalPoints,
    int Percentage,
    bool Passed,
    /// <summary>Empty until submitted — an in-progress attempt has nothing to review.</summary>
    IReadOnlyList<AttemptAnswerResultDto> Results);

// ---- Requests ----

public sealed record CreateQuizRequest(Guid LessonId, string Title, int PassingScore = 70);

public sealed record UpdateQuizRequest(string Title, int PassingScore);

public sealed record AddQuestionRequest(string Prompt, string Type, int Points = 1);

public sealed record UpdateQuestionRequest(string Prompt, int Points, string? Explanation = null);

public sealed record AddChoiceRequest(string Text);

public sealed record SetCorrectChoicesRequest(IReadOnlyList<Guid> CorrectChoiceIds);

public sealed record ReorderRequest(IReadOnlyList<Guid> OrderedIds);

/// <summary>
/// A submission: one entry per answered question.
/// </summary>
/// <param name="Answers">
/// Question id → chosen choice ids. Carries no score — scoring happens in the domain from the answer
/// key, and a score arriving over the wire would make the quiz an honour system with extra steps.
/// </param>
public sealed record SubmitAttemptRequest(IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> Answers);

internal static class AssessmentMapping
{
    public static string ToWire<TEnum>(this TEnum value) where TEnum : struct, Enum =>
        value.ToString().ToLowerInvariant();

    public static QuestionType ParseQuestionType(string? value) =>
        Enum.TryParse<QuestionType>(value, ignoreCase: true, out var parsed)
            ? parsed
            : QuestionType.SingleChoice;

    /// <summary>
    /// The learner projection. Written as one function so there is a single place where a choice
    /// becomes learner-visible, and it is impossible to include correctness from here.
    /// </summary>
    public static QuizDto ToLearnerDto(this Quiz quiz) =>
        new(quiz.Id,
            quiz.LessonId,
            quiz.Title,
            quiz.PassingScore,
            quiz.TotalPoints,
            quiz.Questions
                .Select(q => new QuestionDto(
                    q.Id,
                    q.Prompt,
                    q.Type.ToWire(),
                    q.Points,
                    q.Choices.Select(c => new ChoiceDto(c.Id, c.Text)).ToList()))
                .ToList());

    public static AuthoringQuizDto ToAuthoringDto(this Quiz quiz) =>
        new(quiz.Id,
            quiz.LessonId,
            quiz.Title,
            quiz.Status.ToWire(),
            quiz.PassingScore,
            quiz.TotalPoints,
            quiz.PublishedAt,
            quiz.Questions
                .Select(q => new AuthoringQuestionDto(
                    q.Id,
                    q.Prompt,
                    q.Type.ToWire(),
                    q.Points,
                    q.Explanation,
                    q.Choices.Select(c => new AuthoringChoiceDto(c.Id, c.Text, c.IsCorrect)).ToList()))
                .ToList());
}
