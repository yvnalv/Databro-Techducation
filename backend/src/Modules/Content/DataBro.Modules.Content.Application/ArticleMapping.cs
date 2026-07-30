using DataBro.Modules.Content.Domain;
using DataBro.Platform.Abstractions;

namespace DataBro.Modules.Content.Application;

/// <summary>
/// The out-of-aggregate references an article DTO needs resolved: the author (from Identity via
/// <c>IUserDirectory</c>, ADR-0008) and its taxonomy terms. Bundled into one object so the mapping
/// signatures stay readable as more references appear, and so every lookup is resolved once per
/// request rather than per article.
/// </summary>
internal sealed record ArticleReferences(
    IReadOnlyDictionary<Guid, UserSummary> Authors,
    IReadOnlyDictionary<Guid, Category> Categories,
    IReadOnlyDictionary<Guid, Tag> Tags,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> TagIdsByArticle)
{
    public static ArticleReferences Empty { get; } = new(
        new Dictionary<Guid, UserSummary>(),
        new Dictionary<Guid, Category>(),
        new Dictionary<Guid, Tag>(),
        new Dictionary<Guid, IReadOnlyList<Guid>>());
}

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

    public static CategoryDto ToDto(this Category c) =>
        new(c.Id, c.Slug.Value, c.Name, c.Description, c.ParentId, c.Order);

    public static TaxonomyTermDto ToTermDto(this Category c) => new(c.Id, c.Slug.Value, c.Name);

    public static TaxonomyTermDto ToTermDto(this Tag t) => new(t.Id, t.Slug.Value, t.Name);

    /// <summary>
    /// Enum values cross the wire lowercase so the JSON contract lines up with the TypeScript
    /// unions in @databro/types. Parsing back in is already case-insensitive.
    /// </summary>
    private static string ToWire<TEnum>(this TEnum value) where TEnum : struct, Enum =>
        value.ToString().ToLowerInvariant();

    private static AuthorDto? ResolveAuthor(this ArticleReferences refs, Guid authorId) =>
        refs.Authors.TryGetValue(authorId, out var user)
            ? new AuthorDto(user.Id, user.DisplayName, user.AvatarUrl)
            : null;

    // A soft-deleted or missing category resolves to null rather than a dangling id, matching how
    // an unresolvable author is handled.
    private static TaxonomyTermDto? ResolveCategory(this ArticleReferences refs, Guid? categoryId) =>
        categoryId is { } id && refs.Categories.TryGetValue(id, out var category)
            ? category.ToTermDto()
            : null;

    private static IReadOnlyList<TaxonomyTermDto> ResolveTags(this ArticleReferences refs, Guid articleId)
    {
        if (!refs.TagIdsByArticle.TryGetValue(articleId, out var tagIds))
            return [];

        return tagIds
            .Select(id => refs.Tags.GetValueOrDefault(id))
            .Where(t => t is not null)
            .Select(t => t!.ToTermDto())
            .OrderBy(t => t.Name)
            .ToList();
    }

    public static ArticleSummaryDto ToSummaryDto(this Article a, ArticleReferences refs) =>
        new(a.Id, a.Slug.Value, a.Title, a.Summary, a.Status.ToWire(), a.Visibility.ToWire(),
            a.Locale, refs.ResolveAuthor(a.AuthorId), a.ReadingTimeMinutes, a.PublishedAt,
            refs.ResolveCategory(a.CategoryId), refs.ResolveTags(a.Id));

    public static ArticleDto ToDto(this Article a, ArticleReferences refs)
    {
        // Public read serves the published snapshot; authoring views fall back to the draft.
        var content = (a.PublishedBlocks ?? a.DraftBlocks).ToDto();
        return a.ToDto(refs, content);
    }

    public static ArticleDto ToDraftDto(this Article a, ArticleReferences refs) =>
        a.ToDto(refs, a.DraftBlocks.ToDto());

    private static ArticleDto ToDto(this Article a, ArticleReferences refs, ContentDocumentDto content) =>
        new(a.Id, a.Slug.Value, a.Title, a.Summary, a.Status.ToWire(), a.Visibility.ToWire(),
            a.Locale, refs.ResolveAuthor(a.AuthorId), a.CurrentVersion, a.ReadingTimeMinutes,
            content, a.Seo.ToDto(), a.PublishedAt, a.ScheduledFor,
            refs.ResolveCategory(a.CategoryId), refs.ResolveTags(a.Id));
}
