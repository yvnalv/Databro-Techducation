using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Assessment.Domain;

public enum QuizStatus { Draft, Published, Unpublished }

/// <summary>
/// How a question is answered. Kept small on purpose — three types that score unambiguously beats a
/// dozen where "close enough" needs a judgement call the platform cannot make.
/// </summary>
public enum QuestionType
{
    /// <summary>Exactly one choice is correct.</summary>
    SingleChoice,

    /// <summary>Several may be correct, and the learner must select all of them and nothing else.</summary>
    MultipleChoice,

    /// <summary>Two fixed choices. A single-choice question wearing a convention.</summary>
    TrueFalse,
}

/// <summary>
/// A quiz bound to one lesson.
///
/// <para>
/// The aggregate root, owning its questions and their choices, for the same reason a course owns its
/// curriculum: an author edits the whole thing at once and reordering rewrites every sibling, so one
/// root makes that a single atomic save.
/// </para>
/// <para>
/// <b>The correct answers live here and must never leave through a learner-facing read.</b> That is
/// this module's CT-6: the domain exposes them because scoring needs them, and it is the DTO layer's
/// job to ensure no learner shape ever carries them. Tests pin it, because the failure is silent —
/// a quiz that quietly ships its answer key still looks and works fine.
/// </para>
/// </summary>
public sealed class Quiz : AggregateRoot
{
    private readonly List<Question> _questions = [];

    /// <summary>The lesson this belongs to. An id across a module boundary, never a navigation.</summary>
    public Guid LessonId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Percentage needed to pass, 0–100. Stored on the quiz rather than derived from a global
    /// setting: what counts as passing is a property of the assessment, and a course that wants a
    /// harder one should not have to change the platform.
    /// </summary>
    public int PassingScore { get; private set; }

    public QuizStatus Status { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public IReadOnlyList<Question> Questions => _questions.OrderBy(q => q.Order).ToList();

    /// <summary>Total marks available — the denominator every score is out of.</summary>
    public int TotalPoints => _questions.Sum(q => q.Points);

    private Quiz() { } // EF

    public static Quiz CreateDraft(Guid id, Guid lessonId, string title, int passingScore = 70) =>
        new()
        {
            Id = id,
            LessonId = lessonId,
            Title = title.Trim(),
            PassingScore = Math.Clamp(passingScore, 0, 100),
            Status = QuizStatus.Draft,
        };

    public void Describe(string title, int passingScore)
    {
        Title = title.Trim();
        PassingScore = Math.Clamp(passingScore, 0, 100);
    }

    public Question AddQuestion(Guid questionId, string prompt, QuestionType type, int points = 1)
    {
        var question = new Question(questionId, Id, prompt, type, _questions.Count, points);
        _questions.Add(question);
        Normalise();

        return question;
    }

    public Result RemoveQuestion(Guid questionId)
    {
        var question = _questions.FirstOrDefault(q => q.Id == questionId);
        if (question is null)
            return Result.Failure(Error.NotFound("Question not found in this quiz."));

        _questions.Remove(question);
        Normalise();

        return Result.Success();
    }

    /// <summary>Reorders to match the given ids; unnamed ones keep their relative order.</summary>
    public Result ReorderQuestions(IReadOnlyList<Guid> orderedQuestionIds)
    {
        if (orderedQuestionIds.Distinct().Count() != orderedQuestionIds.Count)
            return Result.Failure(Error.Validation("The same question was listed more than once."));

        if (orderedQuestionIds.Any(id => _questions.All(q => q.Id != id)))
            return Result.Failure(Error.Validation("The order refers to a question that is not in this quiz."));

        var ranked = orderedQuestionIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);

        var sorted = _questions
            .OrderBy(q => ranked.TryGetValue(q.Id, out var rank) ? rank : int.MaxValue)
            .ThenBy(q => q.Order)
            .ToList();

        _questions.Clear();
        _questions.AddRange(sorted);
        Normalise();

        return Result.Success();
    }

    public Question? FindQuestion(Guid questionId) => _questions.FirstOrDefault(q => q.Id == questionId);

    /// <summary>
    /// Publishes the quiz. Every question must be answerable and scorable, which is stricter than a
    /// course's publish rule — a course may go live with lessons still being written, but a quiz with
    /// an unanswerable question is a trap rather than an incomplete offering.
    /// </summary>
    public Result Publish(DateTimeOffset now)
    {
        if (_questions.Count == 0)
            return Result.Failure(Error.Rule("A quiz requires at least one question before it can be published."));

        foreach (var question in _questions)
        {
            var check = question.ValidateForPublish();
            if (check.IsFailure) return check;
        }

        Status = QuizStatus.Published;
        PublishedAt = now;

        return Result.Success();
    }

    public Result Unpublish()
    {
        if (Status != QuizStatus.Published)
            return Result.Failure(Error.Conflict("Only a published quiz can be unpublished."));

        Status = QuizStatus.Unpublished;
        return Result.Success();
    }

    private void Normalise()
    {
        for (var i = 0; i < _questions.Count; i++) _questions[i].SetOrder(i);
    }
}
