namespace DataBro.Modules.Content.Domain;

/// <summary>SEO metadata carried by every content unit (docs/SEO.md §2). Stored as JSONB.</summary>
public sealed class SeoMetadata
{
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
    public string? CanonicalUrl { get; init; }
    public string Robots { get; init; } = "index,follow";
    public string? OgImageMediaId { get; init; }

    public static SeoMetadata Default => new();
}
