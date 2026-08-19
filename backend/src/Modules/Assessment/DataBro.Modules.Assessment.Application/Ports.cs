using DataBro.Modules.Assessment.Domain;
using DataBro.Platform.Results;

namespace DataBro.Modules.Assessment.Application;

/// <summary>Persistence port for the <see cref="Quiz"/> aggregate.</summary>
public interface IQuizRepository
{
    Task AddAsync(Quiz quiz, CancellationToken ct = default);

    /// <summary>The whole quiz — questions and choices — because that is the aggregate.</summary>
    Task<Quiz?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>The published quiz for a lesson, or null. At most one quiz per lesson.</summary>
    Task<Quiz?> GetPublishedForLessonAsync(Guid lessonId, CancellationToken ct = default);

    Task<Quiz?> GetForLessonAsync(Guid lessonId, CancellationToken ct = default);

    Task<PagedResult<Quiz>> ListAllAsync(PageRequest page, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Persistence port for the <see cref="QuizAttempt"/> aggregate.</summary>
public interface IQuizAttemptRepository
{
    Task AddAsync(QuizAttempt attempt, CancellationToken ct = default);

    Task<QuizAttempt?> GetAsync(Guid attemptId, CancellationToken ct = default);

    /// <summary>The learner's attempts at one quiz, newest first.</summary>
    Task<IReadOnlyList<QuizAttempt>> ListForLearnerAsync(
        Guid userId, Guid quizId, CancellationToken ct = default);

    /// <summary>
    /// Every <b>submitted</b> attempt at one quiz, across all learners, newest first — the author's
    /// review of who has been assessed. In-progress attempts are excluded: they have no score to
    /// review and are transient, resumed on the next page load rather than recorded.
    /// </summary>
    Task<IReadOnlyList<QuizAttempt>> ListForQuizAsync(Guid quizId, CancellationToken ct = default);

    /// <summary>
    /// The learner's open attempt at a quiz, if any. Starting a quiz twice should resume rather than
    /// abandon — a page reload is not a decision to throw away answers.
    /// </summary>
    Task<QuizAttempt?> GetOpenAttemptAsync(Guid userId, Guid quizId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
