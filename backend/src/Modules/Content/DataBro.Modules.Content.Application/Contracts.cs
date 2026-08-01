using System.Text.Json.Nodes;

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

public sealed record CategoryDto(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    Guid? ParentId,
    int Order);

/// <summary>A category plus its ancestors (root first) — the breadcrumb trail for a category page.</summary>
public sealed record CategoryWithAncestorsDto(CategoryDto Category, IReadOnlyList<CategoryDto> Ancestors);

public sealed record CreateCategoryRequest(
    string Name,
    string? Slug = null,
    Guid? ParentId = null,
    string? Description = null,
    int Order = 0);

/// <summary>
/// Slug is absent by design: a category slug is a public URL and is immutable until the redirects
/// slice lands (CT-3).
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
    IReadOnlyList<TaxonomyTermDto> Tags);

/// <summary>
/// A page of results. Endpoints put this in the envelope's <c>meta</c> (docs/API_SPEC.md §3), so a
/// client can render crawlable page links without a second request.
/// </summary>
public sealed record PageMetaDto(int Page, int PageSize, int Total, int TotalPages);
