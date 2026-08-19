using DataBro.Modules.Assessment.Application;
using DataBro.Modules.Assessment.Domain;
using DataBro.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Assessment.Infrastructure.Persistence;

internal sealed class QuizRepository(AssessmentDbContext db) : IQuizRepository
{
    /// <summary>Questions and their choices, because that is the aggregate.</summary>
    private IQueryable<Quiz> Full => db.Quizzes.Include("_questions._choices");

    public async Task AddAsync(Quiz quiz, CancellationToken ct = default)
        => await db.Quizzes.AddAsync(quiz, ct);

    public Task<Quiz?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Full.FirstOrDefaultAsync(q => q.Id == id, ct);

    public Task<Quiz?> GetPublishedForLessonAsync(Guid lessonId, CancellationToken ct = default)
        => Full.FirstOrDefaultAsync(q => q.LessonId == lessonId && q.Status == QuizStatus.Published, ct);

    public Task<Quiz?> GetForLessonAsync(Guid lessonId, CancellationToken ct = default)
        => Full.FirstOrDefaultAsync(q => q.LessonId == lessonId, ct);

    public async Task<PagedResult<Quiz>> ListAllAsync(PageRequest page, CancellationToken ct = default)
    {
        var query = Full.OrderByDescending(q => q.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);

        return new PagedResult<Quiz>(items, page.Page, page.PageSize, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

internal sealed class QuizAttemptRepository(AssessmentDbContext db) : IQuizAttemptRepository
{
    private IQueryable<QuizAttempt> Full => db.Attempts.Include("_answers");

    public async Task AddAsync(QuizAttempt attempt, CancellationToken ct = default)
        => await db.Attempts.AddAsync(attempt, ct);

    public Task<QuizAttempt?> GetAsync(Guid attemptId, CancellationToken ct = default)
        => Full.FirstOrDefaultAsync(a => a.Id == attemptId, ct);

    public async Task<IReadOnlyList<QuizAttempt>> ListForLearnerAsync(
        Guid userId, Guid quizId, CancellationToken ct = default)
        => await Full
            .Where(a => a.UserId == userId && a.QuizId == quizId)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(ct);

    // No answer graph: the review is a roll-up of scores, so loading each attempt's selections would
    // be paying for a column the summary never reads.
    public async Task<IReadOnlyList<QuizAttempt>> ListForQuizAsync(Guid quizId, CancellationToken ct = default)
        => await db.Attempts
            .Where(a => a.QuizId == quizId && a.SubmittedAt != null)
            .OrderByDescending(a => a.SubmittedAt)
            .ToListAsync(ct);

    public Task<QuizAttempt?> GetOpenAttemptAsync(Guid userId, Guid quizId, CancellationToken ct = default)
        => Full
            .Where(a => a.UserId == userId && a.QuizId == quizId && a.SubmittedAt == null)
            .OrderByDescending(a => a.StartedAt)
            .FirstOrDefaultAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
