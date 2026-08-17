using DataBro.Modules.Content.Application;
using DataBro.Modules.Content.Domain;
using DataBro.Platform.Results;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

internal sealed class ArticleRepository(ContentDbContext db) : IArticleRepository
{
    public async Task AddAsync(Article article, CancellationToken ct = default)
        => await db.Articles.AddAsync(article, ct);

    public Task<Article?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Articles
            .Include("_versions")
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

    public async Task<PagedResult<Article>> SearchPublishedAsync(
        string query,
        string locale,
        PageRequest page,
        bool fuzzy = false,
        CancellationToken ct = default)
    {
        // Locale-scoped, and not merely as a filter: the row's stemmer was chosen by its locale, so
        // querying with the other configuration would compare differently-stemmed tokens and quietly
        // under-match.
        var config = SearchConfigFor(locale);

        var published = db.Articles.Where(a => a.Status == ArticleStatus.Published && a.Locale == locale);

        var ranked = fuzzy
            // `word_similarity`, not `similarity`. Plain similarity compares whole strings, so it
            // divides by the length of the title: searching "Retreival" against "Retrieval-Augmented
            // Generation, End to End" scores 0.14 and matches nothing, no matter how obvious the
            // typo. `word_similarity` finds the best matching run of words inside the title instead
            // and scores the same pair 0.43 — which is the behaviour a typo fallback needs.
            // Matched against the *published* title, like the tsvector above (CT-6). Using `Title`
            // here made the fuzzy path a way to find a draft headline that full-text correctly
            // refused to index.
            ? published
                .Where(a => EF.Functions.TrigramsWordSimilarity(query, a.PublishedTitle ?? a.Title) > FuzzyThreshold)
                .OrderByDescending(a => EF.Functions.TrigramsWordSimilarity(query, a.PublishedTitle ?? a.Title))
                .ThenByDescending(a => a.PublishedAt)
            // `websearch_to_tsquery` rather than `to_tsquery`: it accepts whatever a person types
            // into a search box — quotes, OR, a stray operator — without throwing, and supports
            // quoted phrases. `to_tsquery` raises a syntax error on input as ordinary as `a & `.
            : published
                .Where(a => EF.Property<NpgsqlTsVector>(a, ArticleConfiguration.SearchVectorProperty)
                    .Matches(EF.Functions.WebSearchToTsQuery(config, query)))
                .OrderByDescending(a => EF.Property<NpgsqlTsVector>(a, ArticleConfiguration.SearchVectorProperty)
                    .Rank(EF.Functions.WebSearchToTsQuery(config, query)))
                .ThenByDescending(a => a.PublishedAt);

        return await PageAsync(ranked, page, ct);
    }

    /// <summary>
    /// Word-similarity floor for the fuzzy fallback. 0.3 is pg_trgm's own default threshold, and
    /// measurement backs it here: a transposed letter scores ~0.43 while unrelated titles score 0,
    /// so the gap is wide. Lower turns the fallback into "here is the catalogue".
    ///
    /// This predicate is not index-accelerated — the GIN trigram index answers the `&lt;%` operator,
    /// which carries its own session-level threshold rather than an explicit one. A sequential scan
    /// is acceptable while the fallback only runs on queries that matched nothing at all; it stops
    /// being acceptable at a catalogue size where a full scan is slow, which is one of the triggers
    /// for the OpenSearch upgrade (ADR-0006).
    /// </summary>
    private const double FuzzyThreshold = 0.3;

    /// <summary>
    /// Must mirror the <c>CASE</c> in <see cref="ArticleConfiguration"/>'s generated vector — the
    /// query has to be stemmed the same way the index was.
    /// </summary>
    private static string SearchConfigFor(string locale)
        => locale == "id" ? "indonesian" : "english";

    public async Task<IReadOnlyList<Article>> ListDueScheduledAsync(DateTimeOffset now, CancellationToken ct = default)
        // Versions loaded so Publish can append the new one against a tracked collection, matching
        // the interactive publish path (GetByIdAsync).
        => await db.Articles
            .Include("_versions")
            .Where(a => a.Status == ArticleStatus.Scheduled && a.ScheduledFor != null && a.ScheduledFor <= now)
            .ToListAsync(ct);

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
