using DataBro.Modules.Content.Domain;

namespace DataBro.Modules.Content.Application;

/// <summary>Persistence port for the Article aggregate. Implemented in the Infrastructure layer.</summary>
public interface IArticleRepository
{
    Task AddAsync(Article article, CancellationToken ct = default);
    Task<Article?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns a published, non-deleted article by slug (public read path), or null.</summary>
    Task<Article?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default);

    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);

    Task<IReadOnlyList<Article>> ListPublishedAsync(int limit, CancellationToken ct = default);
    Task<IReadOnlyList<Article>> ListAllAsync(int limit, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
