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

/// <summary>Persistence port for the <see cref="Enrollment"/> aggregate.</summary>
public interface IEnrollmentRepository
{
    Task AddAsync(Enrollment enrollment, CancellationToken ct = default);

    /// <summary>The learner's enrollment in one course, with its progress rows.</summary>
    Task<Enrollment?> GetAsync(Guid userId, Guid courseId, CancellationToken ct = default);

    /// <summary>
    /// Everything the learner is enrolled in, most recently touched first — the dashboard query.
    /// Progress rows are included because the cards show a completion count.
    /// </summary>
    Task<PagedResult<Enrollment>> ListForUserAsync(
        Guid userId, PageRequest page, CancellationToken ct = default);

    /// <summary>
    /// True when the unique (user, course) index rejected the last save — two concurrent enrol
    /// clicks. Distinguishing that from any other failure is what lets the service recover.
    /// </summary>
    Task<bool> SaveHandlingDuplicateAsync(CancellationToken ct = default);

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
