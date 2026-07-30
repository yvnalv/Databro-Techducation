using DataBro.Modules.Content.Domain;
using DataBro.Platform.Abstractions;

namespace DataBro.Modules.Content.Application;

/// <summary>Mapping between the Article aggregate and the API DTOs.</summary>
internal static class ArticleMapping
{
    public static ContentDocument ToDomain(this ContentDocumentDto dto) =>
        new()
        {
            Version = dto.Version,
            Blocks = dto.Blocks
                .Select(b => new ContentBlock { Id = b.Id, Type = b.Type, Data = b.Data })
                .ToList(),
        };

    public static ContentDocumentDto ToDto(this ContentDocument doc) =>
        new(doc.Version, doc.Blocks.Select(b => new ContentBlockDto(b.Id, b.Type, b.Data)).ToList());

    public static SeoMetadata ToDomain(this SeoDto dto) =>
        new()
        {
            MetaTitle = dto.MetaTitle,
            MetaDescription = dto.MetaDescription,
            CanonicalUrl = dto.CanonicalUrl,
            Robots = string.IsNullOrWhiteSpace(dto.Robots) ? "index,follow" : dto.Robots,
            OgImageMediaId = dto.OgImageMediaId,
        };

    public static SeoDto ToDto(this SeoMetadata seo) =>
        new(seo.MetaTitle, seo.MetaDescription, seo.CanonicalUrl, seo.Robots, seo.OgImageMediaId);

    /// <summary>
    /// Enum values cross the wire lowercase so the JSON contract lines up with the TypeScript
    /// unions in @databro/types. Parsing back in is already case-insensitive.
    /// </summary>
    private static string ToWire<TEnum>(this TEnum value) where TEnum : struct, Enum =>
        value.ToString().ToLowerInvariant();

    private static AuthorDto? Resolve(this IReadOnlyDictionary<Guid, UserSummary> authors, Guid authorId) =>
        authors.TryGetValue(authorId, out var user)
            ? new AuthorDto(user.Id, user.DisplayName, user.AvatarUrl)
            : null;

    public static ArticleSummaryDto ToSummaryDto(
        this Article a, IReadOnlyDictionary<Guid, UserSummary> authors) =>
        new(a.Id, a.Slug.Value, a.Title, a.Summary, a.Status.ToWire(), a.Visibility.ToWire(),
            a.Locale, authors.Resolve(a.AuthorId), a.ReadingTimeMinutes, a.PublishedAt);

    public static ArticleDto ToDto(this Article a, IReadOnlyDictionary<Guid, UserSummary> authors)
    {
        // Public read serves the published snapshot; authoring views fall back to the draft.
        var content = (a.PublishedBlocks ?? a.DraftBlocks).ToDto();
        return new ArticleDto(
            a.Id, a.Slug.Value, a.Title, a.Summary, a.Status.ToWire(), a.Visibility.ToWire(),
            a.Locale, authors.Resolve(a.AuthorId), a.CategoryId, a.CurrentVersion, a.ReadingTimeMinutes,
            content, a.Seo.ToDto(), a.PublishedAt, a.ScheduledFor);
    }

    public static ArticleDto ToDraftDto(
        this Article a, IReadOnlyDictionary<Guid, UserSummary> authors) =>
        new(a.Id, a.Slug.Value, a.Title, a.Summary, a.Status.ToWire(), a.Visibility.ToWire(),
            a.Locale, authors.Resolve(a.AuthorId), a.CategoryId, a.CurrentVersion, a.ReadingTimeMinutes,
            a.DraftBlocks.ToDto(), a.Seo.ToDto(), a.PublishedAt, a.ScheduledFor);
}
