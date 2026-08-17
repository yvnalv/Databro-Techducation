using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Results;

namespace DataBro.Modules.Learning.Application;

/// <summary>Persistence port for the <see cref="Course"/> aggregate.</summary>
public interface ICourseRepository
{
    Task AddAsync(Course course, CancellationToken ct = default);

    /// <summary>The whole curriculum — modules and their lessons — because that is the aggregate.</summary>
    Task<Course?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Course?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default);

    Task<bool> SlugExistsAsync(string slug, Guid? excluding = null, CancellationToken ct = default);

    Task<PagedResult<Course>> ListPublishedAsync(PageRequest page, CancellationToken ct = default);

    Task<PagedResult<Course>> ListAllAsync(PageRequest page, CancellationToken ct = default);

    /// <summary>Resolves several courses at once — what a path page needs to render its cards.</summary>
    Task<IReadOnlyList<Course>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Persistence port for the <see cref="LearningPath"/> aggregate.</summary>
public interface ILearningPathRepository
{
    Task AddAsync(LearningPath path, CancellationToken ct = default);

    Task<LearningPath?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<LearningPath?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default);

    Task<bool> SlugExistsAsync(string slug, Guid? excluding = null, CancellationToken ct = default);

    Task<PagedResult<LearningPath>> ListPublishedAsync(PageRequest page, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
