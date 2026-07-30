using DataBro.Modules.Content.Application;
using DataBro.Modules.Content.Domain;
using DataBro.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

internal sealed class ArticleRepository(ContentDbContext db) : IArticleRepository
{
    public async Task AddAsync(Article article, CancellationToken ct = default)
        => await db.Articles.AddAsync(article, ct);

    public Task<Article?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Articles
            .Include(a => a.Versions)
            .Include("_tags")
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Article?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (!SlugParser.TryParse(slug, out var parsed))
            return Task.FromResult<Article?>(null);

        return db.Articles
            .Include("_tags")
            .FirstOrDefaultAsync(a => a.Slug == parsed && a.Status == ArticleStatus.Published, ct);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
    {
        if (!SlugParser.TryParse(slug, out var parsed))
            return Task.FromResult(false);

        return db.Articles.AnyAsync(a => a.Slug == parsed, ct);
    }

    public async Task<PagedResult<Article>> ListPublishedAsync(
        PageRequest page,
        Guid? categoryId = null,
        Guid? tagId = null,
        CancellationToken ct = default)
    {
        var query = db.Articles.Where(a => a.Status == ArticleStatus.Published);

        if (categoryId is { } category)
            query = query.Where(a => a.CategoryId == category);

        if (tagId is { } tag)
        {
            // Filter through the join without exposing a navigation from Article to Tag.
            var taggedIds = db.ArticleTags.Where(at => at.TagId == tag).Select(at => at.ArticleId);
            query = query.Where(a => taggedIds.Contains(a.Id));
        }

        return await PageAsync(query.OrderByDescending(a => a.PublishedAt), page, ct);
    }

    public async Task<PagedResult<Article>> ListAllAsync(PageRequest page, CancellationToken ct = default)
        => await PageAsync(db.Articles.OrderByDescending(a => a.CreatedAt), page, ct);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetTagIdsAsync(
        IReadOnlyCollection<Guid> articleIds, CancellationToken ct = default)
    {
        if (articleIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<Guid>>();

        var distinct = articleIds.Distinct().ToArray();

        // Joined against Tags rather than read straight off article_tags: the global soft-delete
        // filter applies to Tags, so a deleted tag drops out here instead of surfacing as a dangling
        // id on a public page.
        var links = await db.ArticleTags
            .Where(at => distinct.Contains(at.ArticleId))
            .Join(db.Tags, at => at.TagId, t => t.Id, (at, t) => new { at.ArticleId, TagId = t.Id })
            .ToListAsync(ct);

        return links
            .GroupBy(l => l.ArticleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(l => l.TagId).ToList());
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    private static async Task<PagedResult<Article>> PageAsync(
        IOrderedQueryable<Article> query, PageRequest page, CancellationToken ct)
    {
        // Counted before paging so `total` reflects the whole result set, which is what drives the
        // crawlable page links on taxonomy pages.
        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);

        return new PagedResult<Article>(items, page.Page, page.PageSize, total);
    }
}
