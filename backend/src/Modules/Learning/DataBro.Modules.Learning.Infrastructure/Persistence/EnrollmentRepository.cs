using DataBro.Modules.Learning.Application;
using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Results;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DataBro.Modules.Learning.Infrastructure.Persistence;

internal sealed class EnrollmentRepository(LearningDbContext db) : IEnrollmentRepository
{
    /// <summary>
    /// An enrollment is always loaded with its progress: every operation on it either reads the
    /// completed set or writes into it, and the row count is bounded by the lessons one learner has
    /// touched in one course.
    /// </summary>
    private IQueryable<Enrollment> Full => db.Enrollments.Include("_progress");

    public async Task AddAsync(Enrollment enrollment, CancellationToken ct = default)
        => await db.Enrollments.AddAsync(enrollment, ct);

    public Task<Enrollment?> GetAsync(Guid userId, Guid courseId, CancellationToken ct = default)
        => Full.FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == courseId, ct);

    public async Task<PagedResult<Enrollment>> ListForUserAsync(
        Guid userId, PageRequest page, CancellationToken ct = default)
    {
        // Ordered by last activity, falling back to when they joined: an enrollment nobody has
        // opened yet has no LastAccessedAt, and it should sit with the recent ones rather than at
        // the bottom — a course you signed up for and have not started is exactly what a dashboard
        // should surface.
        var query = Full
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.LastAccessedAt ?? e.EnrolledAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Enrollment>(items, page.Page, page.PageSize, total);
    }

    public async Task<bool> SaveHandlingDuplicateAsync(CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        })
        {
            // The other click won. Detach so the caller can re-read cleanly — a failed insert left
            // tracked would be retried on the next save and fail again.
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
