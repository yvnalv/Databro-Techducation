using DataBro.Modules.Content.Domain;

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

    public static ArticleSummaryDto ToSummaryDto(this Article a) =>
        new(a.Id, a.Slug.Value, a.Title, a.Summary, a.Status.ToString(), a.Visibility.ToString(),
            a.Locale, a.AuthorId, a.ReadingTimeMinutes, a.PublishedAt);

    public static ArticleDto ToDto(this Article a)
    {
        // Public read serves the published snapshot; authoring views fall back to the draft.
        var content = (a.PublishedBlocks ?? a.DraftBlocks).ToDto();
        return new ArticleDto(
            a.Id, a.Slug.Value, a.Title, a.Summary, a.Status.ToString(), a.Visibility.ToString(),
            a.Locale, a.AuthorId, a.CategoryId, a.CurrentVersion, a.ReadingTimeMinutes,
            content, a.Seo.ToDto(), a.PublishedAt, a.ScheduledFor);
    }

    public static ArticleDto ToDraftDto(this Article a) =>
        new(a.Id, a.Slug.Value, a.Title, a.Summary, a.Status.ToString(), a.Visibility.ToString(),
            a.Locale, a.AuthorId, a.CategoryId, a.CurrentVersion, a.ReadingTimeMinutes,
            a.DraftBlocks.ToDto(), a.Seo.ToDto(), a.PublishedAt, a.ScheduledFor);
}
