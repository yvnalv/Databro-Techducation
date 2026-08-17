using DataBro.Modules.Content.Application;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Results;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

/// <summary>
/// Content's segment of the search results (ADR-0014).
///
/// A thin adapter over the article search that already existed (ADR-0010) — the index, the ranking
/// and the typo fallback are unchanged. All that is new is presenting them as one segment among
/// several instead of as the whole answer.
/// </summary>
internal sealed class ArticleSearch(ArticleService articles) : IModuleSearch
{
    public string Kind => "articles";

    /// <summary>After courses.</summary>
    public int Order => 10;

    public async Task<SearchSegment> SearchAsync(
        string query, string locale, int limit, CancellationToken ct = default)
    {
        var result = await articles.SearchAsync(query, locale, new PageRequest(1, limit), ct);

        return new SearchSegment(
            Kind,
            result.Results.Items
                .Select(a => new SearchHit(a.Id, a.Slug, $"/articles/{a.Slug}", a.Title, a.Summary))
                .ToList(),
            // The service's own total, not the page size: a segment showing five of forty should say
            // forty, or "showing 5" is the only honest thing the UI could write and it would be wrong.
            result.Results.Total,
            result.MatchMode);
    }
}
