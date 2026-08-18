using DataBro.Modules.Learning.Application;
using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Learning.Infrastructure.Persistence;

internal sealed class CourseRepository(LearningDbContext db) : ICourseRepository
{
    /// <summary>
    /// The whole curriculum. Two levels of include on every load of the aggregate, because the
    /// aggregate *is* the curriculum — a course without its modules cannot enforce ordering, which
    /// is the invariant the root exists to hold (ADR-0013).
    /// </summary>
    private IQueryable<Course> Full =>
        db.Courses.Include("_modules._lessons");

    public async Task AddAsync(Course course, CancellationToken ct = default)
        => await db.Courses.AddAsync(course, ct);

    public Task<Course?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Full.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Course?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (!TryParseSlug(slug, out var parsed))
            return Task.FromResult<Course?>(null);

        return Full.FirstOrDefaultAsync(c => c.Slug == parsed && c.Status == CourseStatus.Published, ct);
    }

    public Task<bool> SlugExistsAsync(string slug, Guid? excluding = null, CancellationToken ct = default)
    {
        if (!TryParseSlug(slug, out var parsed))
            return Task.FromResult(false);

        return db.Courses.AnyAsync(c => c.Slug == parsed && c.Id != excluding, ct);
    }

    public async Task<PagedResult<Course>> ListPublishedAsync(PageRequest page, CancellationToken ct = default)
        => await PageAsync(
            Full.Where(c => c.Status == CourseStatus.Published).OrderByDescending(c => c.PublishedAt), page, ct);

    public async Task<PagedResult<Course>> ListAllAsync(PageRequest page, CancellationToken ct = default)
        => await PageAsync(Full.OrderByDescending(c => c.CreatedAt), page, ct);

    public async Task<IReadOnlyList<Course>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];

        var distinct = ids.Distinct().ToArray();
        return await Full.Where(c => distinct.Contains(c.Id)).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    private static async Task<PagedResult<Course>> PageAsync(
        IOrderedQueryable<Course> query, PageRequest page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);

        return new PagedResult<Course>(items, page.Page, page.PageSize, total);
    }

    /// <summary>
    /// A malformed slug in a URL is a 404, not an exception. <see cref="Slug.Create"/> throws by
    /// design, so callers that are matching rather than creating need this.
    /// </summary>
    internal static bool TryParseSlug(string value, out Slug slug)
    {
        try
        {
            slug = Slug.Create(value);
            return true;
        }
        catch (ArgumentException)
        {
            slug = null!;
            return false;
        }
    }
}

internal sealed class LearningPathRepository(LearningDbContext db) : ILearningPathRepository
{
    private IQueryable<LearningPath> Full => db.LearningPaths.Include("_courses");

    public async Task AddAsync(LearningPath path, CancellationToken ct = default)
        => await db.LearningPaths.AddAsync(path, ct);

    public Task<LearningPath?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Full.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<LearningPath?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (!CourseRepository.TryParseSlug(slug, out var parsed))
            return Task.FromResult<LearningPath?>(null);

        return Full.FirstOrDefaultAsync(p => p.Slug == parsed && p.Status == CourseStatus.Published, ct);
    }

    public Task<bool> SlugExistsAsync(string slug, Guid? excluding = null, CancellationToken ct = default)
    {
        if (!CourseRepository.TryParseSlug(slug, out var parsed))
            return Task.FromResult(false);

        return db.LearningPaths.AnyAsync(p => p.Slug == parsed && p.Id != excluding, ct);
    }

    public async Task<PagedResult<LearningPath>> ListPublishedAsync(PageRequest page, CancellationToken ct = default)
    {
        var query = Full.Where(p => p.Status == CourseStatus.Published).OrderByDescending(p => p.PublishedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);

        return new PagedResult<LearningPath>(items, page.Page, page.PageSize, total);
    }

    public async Task<PagedResult<LearningPath>> ListAllAsync(PageRequest page, CancellationToken ct = default)
    {
        // Newest first by creation, not by publish date: a curator's listing is dominated by drafts,
        // which have no publish date at all.
        var query = Full.OrderByDescending(p => p.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);

        return new PagedResult<LearningPath>(items, page.Page, page.PageSize, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
