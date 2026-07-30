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

public sealed record ArticleSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string Status,
    string Visibility,
    string Locale,
    Guid AuthorId,
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
    Guid AuthorId,
    Guid? CategoryId,
    int CurrentVersion,
    int ReadingTimeMinutes,
    ContentDocumentDto Content,
    SeoDto Seo,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ScheduledFor);
