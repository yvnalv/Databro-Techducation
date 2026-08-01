using DataBro.Modules.Content.Application;
using DataBro.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

internal sealed class CategoryRepository(ContentDbContext db) : ICategoryRepository
{
    public async Task AddAsync(Category category, CancellationToken ct = default)
        => await db.Categories.AddAsync(category, ct);

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => SlugParser.TryParse(slug, out var parsed)
            ? db.Categories.FirstOrDefaultAsync(c => c.Slug == parsed, ct)
            : Task.FromResult<Category?>(null);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => SlugParser.TryParse(slug, out var parsed)
            ? db.Categories.AnyAsync(c => c.Slug == parsed, ct)
            : Task.FromResult(false);

    public async Task<IReadOnlyList<Category>> ListAllAsync(CancellationToken ct = default)
        => await db.Categories
            .OrderBy(c => c.Order).ThenBy(c => c.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetAncestryAsync(Guid categoryId, CancellationToken ct = default)
    {
        // The tree is small and fully cacheable, so walking it in memory beats a recursive CTE and
        // keeps the query provider-agnostic.
        var parents = await db.Categories
            .AsNoTracking()
            .Select(c => new { c.Id, c.ParentId })
            .ToDictionaryAsync(c => c.Id, c => c.ParentId, ct);

        var ancestry = new List<Guid>();
        var seen = new HashSet<Guid>();
        var current = parents.GetValueOrDefault(categoryId);

        // `seen` also guards against a cycle already present in the data, so a corrupt row cannot
        // spin this loop forever.
        while (current is { } id && seen.Add(id))
        {
            ancestry.Add(id);
            current = parents.GetValueOrDefault(id);
        }

        return ancestry;
    }

    public Task<int> CountArticlesAsync(Guid categoryId, CancellationToken ct = default)
        => db.Articles.CountAsync(a => a.CategoryId == categoryId, ct);

    public async Task<IReadOnlyDictionary<Guid, int>> CountPublishedArticlesAsync(
        CancellationToken ct = default)
    {
        var counts = await db.Articles
            .Where(a => a.Status == ArticleStatus.Published && a.CategoryId != null)
            .GroupBy(a => a.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return counts.ToDictionary(c => c.CategoryId, c => c.Count);
    }

    public void Remove(Category category) => db.Categories.Remove(category);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

internal sealed class TagRepository(ContentDbContext db) : ITagRepository
{
    public async Task AddAsync(Tag tag, CancellationToken ct = default)
        => await db.Tags.AddAsync(tag, ct);

    public Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tag?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => SlugParser.TryParse(slug, out var parsed)
            ? db.Tags.FirstOrDefaultAsync(t => t.Slug == parsed, ct)
            : Task.FromResult<Tag?>(null);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => SlugParser.TryParse(slug, out var parsed)
            ? db.Tags.AnyAsync(t => t.Slug == parsed, ct)
            : Task.FromResult(false);

    public async Task<IReadOnlyList<Tag>> ListAllAsync(CancellationToken ct = default)
        => await db.Tags.OrderBy(t => t.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Tag>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];

        var distinct = ids.Distinct().ToArray();

        // The global soft-delete filter applies here, so a deleted tag simply does not come back
        // and cannot leak onto a public article page.
        return await db.Tags.Where(t => distinct.Contains(t.Id)).ToListAsync(ct);
    }

    public void Remove(Tag tag) => db.Tags.Remove(tag);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

/// <summary>Parses a user-supplied slug without throwing — an invalid slug is simply "no match".</summary>
internal static class SlugParser
{
    public static bool TryParse(string slug, out Slug parsed)
    {
        try
        {
            parsed = Slug.Create(slug);
            return true;
        }
        catch (ArgumentException)
        {
            parsed = null!;
            return false;
        }
    }
}
