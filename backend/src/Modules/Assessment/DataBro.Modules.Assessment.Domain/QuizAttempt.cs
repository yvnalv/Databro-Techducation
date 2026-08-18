using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Assessment.Domain;

/// <summary>
/// One learner's run at one quiz.
///
/// <para>
/// Its own aggregate root, not part of <see cref="Quiz"/> — the same split as Enrollment against
/// Course, and for the same reason. A quiz is authored rarely by one person; attempts are written
/// constantly by many learners, each touching only their own. Folding them together would make
/// submitting an answer load an entire question bank and put every learner in contention over one
/// aggregate.
/// </para>
/// <para>
/// <b>Attempts are kept, never overwritten.</b> A retake is a new attempt, so the history of what
/// someone actually answered survives — which is the whole value of recording it.
/// </para>
/// </summary>
public sealed class QuizAttempt : AggregateRoot
{
    private readonly List<AttemptAnswer> _answers = [];

    public Guid QuizId { get; private set; }
    public Guid UserId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }

    /// <summary>Marks earned. Meaningless until <see cref="SubmittedAt"/> is set.</summary>
    public int Score { get; private set; }

    public int TotalPoints { get; private set; }

    /// <summary>
    /// Whether the learner passed, decided at submit time against the quiz's threshold *then*.
    ///
    /// <para>
    /// Stored, not derived, for the reason LN-6 stores course completion: an author who later raises
    /// the passing score must not retroactively fail people who had passed under the old one.
    /// </para>
    /// </summary>
    public bool Passed { get; private set; }

    public bool IsSubmitted => SubmittedAt is not null;

    public IReadOnlyList<AttemptAnswer> Answers => _answers.AsReadOnly();

    /// <summary>Percentage, rounded. Zero-point quizzes cannot be published, so the guard is belt-and-braces.</summary>
    public int Percentage => TotalPoints == 0 ? 0 : (int)Math.Round(Score * 100.0 / TotalPoints);

    private QuizAttempt() { } // EF

    public static QuizAttempt Start(Guid id, Guid quizId, Guid userId, DateTimeOffset now) =>
        new()
        {
            Id = id,
            QuizId = quizId,
            UserId = userId,
            StartedAt = now,
        };

    /// <summary>
    /// Scores the attempt against the quiz and closes it.
    ///
    /// <para>
    /// The quiz is passed in rather than held as a navigation, because it belongs to a different
    /// aggregate. Scoring happens <b>here</b>, in the domain, from the answer key — never from
    /// anything a client sent. A submitted score arriving over the wire would make the quiz an
    /// honour system with extra steps.
    /// </para>
    /// <para>
    /// Refuses a second submit: an attempt is a record of one run. Retaking means starting another.
    /// </para>
    /// </summary>
    public Result Submit(Quiz quiz, IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> selections, DateTimeOffset now)
    {
        if (IsSubmitted)
            return Result.Failure(Error.Conflict("This attempt has already been submitted."));

        if (quiz.Id != QuizId)
            return Result.Failure(Error.Validation("That quiz does not match this attempt."));

        _answers.Clear();
        var earned = 0;

        foreach (var question in quiz.Questions)
        {
            // An unanswered question is recorded as an empty answer rather than skipped: "they left
            // it blank" and "the question was not shown" are different facts, and only one of them
            // is true here.
            var selected = selections.TryGetValue(question.Id, out var picked)
                ? picked
                : [];

            // Choices that are not this question's are dropped rather than rejected. A client
            // sending them is confused, not malicious, and refusing the whole submission would cost
            // a learner their attempt over it.
            var valid = selected
                .Where(id => question.Choices.Any(c => c.Id == id))
                .Distinct()
                .ToArray();

            var points = question.Score(valid);
            earned += points;

            _answers.Add(new AttemptAnswer(Guid.NewGuid(), Id, question.Id, valid, points));
        }

        Score = earned;
        TotalPoints = quiz.TotalPoints;
        Passed = Percentage >= quiz.PassingScore;
        SubmittedAt = now;

        Raise(new QuizAttemptSubmittedDomainEvent(Id, UserId, QuizId, quiz.LessonId, Score, TotalPoints, Passed, now));

        return Result.Success();
    }
}

/// <summary>What the learner selected for one question, and what it earned.</summary>
public sealed class AttemptAnswer : Entity
{
    private readonly List<Guid> _selectedChoiceIds = [];

    public Guid AttemptId { get; private set; }
    public Guid QuestionId { get; private set; }

    /// <summary>Empty when the question was left unanswered.</summary>
    public IReadOnlyList<Guid> SelectedChoiceIds => _selectedChoiceIds.AsReadOnly();

    public int PointsEarned { get; private set; }

    private AttemptAnswer() { } // EF

    internal AttemptAnswer(Guid id, Guid attemptId, Guid questionId, IEnumerable<Guid> selected, int pointsEarned)
        : base(id)
    {
        AttemptId = attemptId;
        QuestionId = questionId;
        _selectedChoiceIds.AddRange(selected);
        PointsEarned = pointsEarned;
    }
}
