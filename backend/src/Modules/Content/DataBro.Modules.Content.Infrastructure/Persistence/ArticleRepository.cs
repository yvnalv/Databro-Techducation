using DataBro.Modules.Content.Application;
using DataBro.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

internal sealed class ArticleRepository(ContentDbContext db) : IArticleRepository
{
    public async Task AddAsync(Article article, CancellationToken ct = default)
        => await db.Articles.AddAsync(article, ct);

    public Task<Article?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Articles.Include(a => a.Versions).FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Article?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (!TryParseSlug(slug, out var parsed))
            return Task.FromResult<Article?>(null);

        return db.Articles.FirstOrDefaultAsync(
            a => a.Slug == parsed && a.Status == ArticleStatus.Published, ct);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
    {
        if (!TryParseSlug(slug, out var parsed))
            return Task.FromResult(false);

        return db.Articles.AnyAsync(a => a.Slug == parsed, ct);
    }

    public async Task<IReadOnlyList<Article>> ListPublishedAsync(int limit, CancellationToken ct = default)
        => await db.Articles
            .Where(a => a.Status == ArticleStatus.Published)
            .OrderByDescending(a => a.PublishedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Article>> ListAllAsync(int limit, CancellationToken ct = default)
        => await db.Articles
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    private static bool TryParseSlug(string slug, out Slug parsed)
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
