using DataBro.Modules.Content.Domain;

namespace DataBro.Modules.Content.Application;

/// <summary>Persistence port for the Category aggregate. Implemented in the Infrastructure layer.</summary>
public interface ICategoryRepository
{
    Task AddAsync(Category category, CancellationToken ct = default);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);

    /// <summary>The whole tree, ordered for navigation. Small and cache-friendly by nature.</summary>
    Task<IReadOnlyList<Category>> ListAllAsync(CancellationToken ct = default);

    /// <summary>
    /// The ancestor ids of a category, nearest first. Supplied to <see cref="Category.MoveTo"/> so
    /// the domain can reject cycles (TX-3) without querying.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAncestryAsync(Guid categoryId, CancellationToken ct = default);

    /// <summary>Number of non-deleted articles referencing this category — TX-2 guards deletion.</summary>
    Task<int> CountArticlesAsync(Guid categoryId, CancellationToken ct = default);

    void Remove(Category category);
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Persistence port for the Tag aggregate.</summary>
public interface ITagRepository
{
    Task AddAsync(Tag tag, CancellationToken ct = default);
    Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tag?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Tag>> ListAllAsync(CancellationToken ct = default);

    /// <summary>Resolves ids to tags, skipping any that do not exist or are soft-deleted.</summary>
    Task<IReadOnlyList<Tag>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    void Remove(Tag tag);
    Task SaveChangesAsync(CancellationToken ct = default);
}
