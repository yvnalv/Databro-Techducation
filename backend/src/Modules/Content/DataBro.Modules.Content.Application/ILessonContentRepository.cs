using DataBro.Modules.Content.Domain;
using DataBro.Platform.Results;

namespace DataBro.Modules.Content.Application;

/// <summary>Persistence port for the <see cref="LessonContent"/> aggregate (ADR-0012).</summary>
public interface ILessonContentRepository
{
    Task AddAsync(LessonContent lesson, CancellationToken ct = default);

    Task<LessonContent?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Every lesson body, newest first — the CMS picker's list when attaching one to a course.
    /// Unlike articles there is no public listing counterpart: a lesson body is reached through its
    /// course, never browsed on its own.
    /// </summary>
    Task<PagedResult<LessonContent>> ListAllAsync(PageRequest page, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
