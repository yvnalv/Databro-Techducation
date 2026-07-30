using DataBro.Modules.Content.Domain;
using DataBro.Platform.Results;

namespace DataBro.Modules.Content.Application;

/// <summary>Persistence port for the Article aggregate. Implemented in the Infrastructure layer.</summary>
public interface IArticleRepository
{
    Task AddAsync(Article article, CancellationToken ct = default);
    Task<Article?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns a published, non-deleted article by slug (public read path), or null.</summary>
    Task<Article?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default);

    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Published articles, newest first, optionally narrowed to a category or a tag. Paged because
    /// taxonomy listings are indexable and must expose stable page URLs (see <see cref="PagedResult{T}"/>).
    /// </summary>
    Task<PagedResult<Article>> ListPublishedAsync(
        PageRequest page,
        Guid? categoryId = null,
        Guid? tagId = null,
        CancellationToken ct = default);

    Task<PagedResult<Article>> ListAllAsync(PageRequest page, CancellationToken ct = default);

    /// <summary>
    /// Tag ids per article, excluding soft-deleted tags. Batched to keep list endpoints off the
    /// N+1 path.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetTagIdsAsync(
        IReadOnlyCollection<Guid> articleIds,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
