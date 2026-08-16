using System.Text.Json.Nodes;
using DataBro.Platform.Results;

namespace DataBro.Modules.Content.Application;

// DTOs exchanged with the API layer. Kept separate from domain types (docs/CODING_STANDARDS.md).

public sealed record ContentBlockDto(string Id, string Type, JsonObject? Data);

public sealed record ContentDocumentDto(int Version, IReadOnlyList<ContentBlockDto> Blocks)
{
    public static ContentDocumentDto Empty { get; } = new(1, []);
}

public sealed record SeoDto(
    string? MetaTitle,
    string? MetaDescription,
    string? CanonicalUrl,
    string Robots,
    string? OgImageMediaId);

/// <summary>
/// The byline as the read surface exposes it. Resolved through the shared
/// <c>IUserDirectory</c> contract (ADR-0008) — Content stores only an author id.
/// Null when the author can no longer be resolved (e.g. a deleted account); the client renders
/// its own localized fallback rather than the API inventing user-facing English.
/// </summary>
public sealed record AuthorDto(Guid Id, string DisplayName, string? AvatarUrl);

/// <summary>
/// The byline plus the bio an author card renders. Only the article *detail* response carries it:
/// a list of 20 summaries has no use for 20 bios, and this is the cached, read-heavy public path.
/// </summary>
public sealed record AuthorProfileDto(Guid Id, string DisplayName, string? AvatarUrl, string? Bio);

/// <summary>A category or tag as the read surface exposes it.</summary>
public sealed record TaxonomyTermDto(Guid Id, string Slug, string Name);

/// <summary>A resolved redirect (docs/SEO.md §4). The site serves <c>StatusCode</c> to <c>ToPath</c>.</summary>
public sealed record RedirectDto(string FromPath, string ToPath, int StatusCode);

/// <summary>Body of a slug-change request: the single new slug for an article or taxonomy term.</summary>
public sealed record ChangeSlugRequest(string Slug);

/// <summary>Body of a schedule request: when the article should publish automatically (CT-7).</summary>
public sealed record ScheduleArticleRequest(DateTimeOffset ScheduledFor);

public sealed record CategoryDto(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    Guid? ParentId,
    int Order,
    /// <summary>Published articles in this category. Drives the public category tiles.</summary>
    int ArticleCount = 0);

/// <summary>A category plus its ancestors (root first) — the breadcrumb trail for a category page.</summary>
public sealed record CategoryWithAncestorsDto(CategoryDto Category, IReadOnlyList<CategoryDto> Ancestors);

public sealed record CreateCategoryRequest(
    string Name,
    string? Slug = null,
    Guid? ParentId = null,
    string? Description = null,
    int Order = 0);

/// <summary>
/// Slug is absent by design: it moves through the dedicated slug-change endpoint, which pairs the
/// change with a 301 redirect (CT-3). This request only renames and repositions.
/// </summary>
public sealed record UpdateCategoryRequest(
    string Name,
    string? Description = null,
    int Order = 0,
    Guid? ParentId = null);

public sealed record CreateTagRequest(string Name, string? Slug = null);

public sealed record UpdateTagRequest(string Name);

public sealed record CreateArticleRequest(
    string Title,
    string Summary,
    ContentDocumentDto Content,
    string? Slug = null,
    Guid? AuthorId = null,
    string Locale = "en",
    string Visibility = "public",
    SeoDto? Seo = null,
    Guid? CategoryId = null,
    IReadOnlyList<Guid>? TagIds = null);

public sealed record UpdateArticleRequest(
    string Title,
    string Summary,
    ContentDocumentDto Content,
    SeoDto? Seo = null,
    Guid? CategoryId = null,
    IReadOnlyList<Guid>? TagIds = null);

// `Status` and `Visibility` go over the wire lowercase ("published", "premium") so the JSON
// contract matches the discriminated unions in @databro/types. See ArticleMapping.ToWire.

public sealed record ArticleSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string Status,
    string Visibility,
    string Locale,
    AuthorDto? Author,
    int ReadingTimeMinutes,
    DateTimeOffset? PublishedAt,
    TaxonomyTermDto? Category,
    IReadOnlyList<TaxonomyTermDto> Tags);

public sealed record MediaVariantRefDto(string Name, string Url, int Width, int Height);

/// <summary>
/// A media reference resolved for rendering. Content stores ids; this is the id turned into
/// something an <c>&lt;img&gt;</c> can use, fetched through <c>IMediaDirectory</c> (ADR-0008).
/// </summary>
public sealed record MediaRefDto(
    string Url,
    string AltText,
    int Width,
    int Height,
    IReadOnlyList<MediaVariantRefDto> Variants);

public sealed record ArticleDto(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string Status,
    string Visibility,
    string Locale,
    AuthorProfileDto? Author,
    int CurrentVersion,
    int ReadingTimeMinutes,
    ContentDocumentDto Content,
    SeoDto Seo,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ScheduledFor,
    TaxonomyTermDto? Category,
    IReadOnlyList<TaxonomyTermDto> Tags,
    /// <summary>
    /// Every media id this article references — image blocks and <c>og:image</c> — resolved to URLs
    /// and keyed by id. Shipped with the article so the renderer needs no second request and no
    /// client-side waterfall. An id absent from the map is one whose asset is gone; the renderer
    /// falls back to a placeholder rather than a broken image.
    /// </summary>
    IReadOnlyDictionary<string, MediaRefDto> Media);

/// <summary>
/// A page of results. Endpoints put this in the envelope's <c>meta</c> (docs/API_SPEC.md §3), so a
/// client can render crawlable page links without a second request.
/// </summary>
public sealed record PageMetaDto(int Page, int PageSize, int Total, int TotalPages);

/// <summary>How a set of search results was matched. Goes over the wire so the UI can be honest.</summary>
public static class SearchMatchModes
{
    /// <summary>Full-text match on the query as typed.</summary>
    public const string Exact = "exact";

    /// <summary>
    /// Trigram-similarity match on titles, used only after full-text found nothing. The UI must say
    /// so — silently showing approximate results for a query that matched nothing is how a search
    /// box teaches people not to trust it.
    /// </summary>
    public const string Fuzzy = "fuzzy";
}

/// <summary>A page of search results plus how they were matched (<see cref="SearchMatchModes"/>).</summary>
public sealed record SearchResultDto(PagedResult<ArticleSummaryDto> Results, string MatchMode);
