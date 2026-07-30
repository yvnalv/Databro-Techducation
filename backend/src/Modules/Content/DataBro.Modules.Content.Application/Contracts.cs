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

public sealed record CreateArticleRequest(
    string Title,
    string Summary,
    ContentDocumentDto Content,
    string? Slug = null,
    Guid? AuthorId = null,
    string Locale = "en",
    string Visibility = "public",
    SeoDto? Seo = null);

public sealed record UpdateArticleRequest(
    string Title,
    string Summary,
    ContentDocumentDto Content,
    SeoDto? Seo = null);

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
    DateTimeOffset? PublishedAt);

public sealed record ArticleDto(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string Status,
    string Visibility,
    string Locale,
    AuthorDto? Author,
    Guid? CategoryId,
    int CurrentVersion,
    int ReadingTimeMinutes,
    ContentDocumentDto Content,
    SeoDto Seo,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ScheduledFor);
