namespace DataBro.Modules.Content.Domain;

/// <summary>
/// A standalone, SEO-oriented content unit (docs/CONTENT_MODEL.md §1).
///
/// <para>
/// The block model, version history and the draft → scheduled → published state machine all live on
/// <see cref="ContentUnit"/> — an article is that engine plus the things only a standalone,
/// discoverable page has: an author byline, taxonomy, SEO metadata, and a locale it can be
/// translated from. A lesson body reuses the same engine and has none of these (ADR-0012).
/// </para>
/// </summary>
public sealed class Article : ContentUnit
{
    private readonly List<ArticleVersion> _versions = [];
    private readonly List<ArticleTag> _tags = [];

    public Visibility Visibility { get; private set; }
    public string Locale { get; private set; } = "en";
    public Guid? TranslationGroupId { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public SeoMetadata Seo { get; private set; } = SeoMetadata.Default;

    /// <summary>Tag ids assigned to this article (CT-11: any number).</summary>
    public IReadOnlyList<Guid> TagIds => _tags.Select(t => t.TagId).ToList();

    private Article() { } // EF

    public static Article CreateDraft(
        Guid id,
        Slug slug,
        string title,
        string summary,
        Guid authorId,
        ContentDocument blocks,
        string locale = "en",
        Visibility visibility = Visibility.Public,
        SeoMetadata? seo = null)
    {
        var article = new Article
        {
            AuthorId = authorId,
            Locale = locale,
            Visibility = visibility,
            Seo = seo ?? SeoMetadata.Default,
        };

        article.InitialiseDraft(id, slug, title, summary, blocks);
        return article;
    }

    /// <summary>
    /// Updates the draft, including the SEO metadata only an article carries. The body, title and
    /// summary are the engine's business; this adds the part that is article-specific.
    /// </summary>
    public void UpdateDraft(string title, string summary, ContentDocument blocks, SeoMetadata? seo = null)
    {
        base.UpdateDraft(title, summary, blocks);
        if (seo is not null) Seo = seo;
    }

    /// <summary>
    /// Assigns the article's single category (CT-11), or clears it. The category's existence is
    /// verified by the application layer — the domain cannot query.
    /// </summary>
    public void SetCategory(Guid? categoryId) => CategoryId = categoryId;

    /// <summary>
    /// Replaces the article's tag set (CT-11: any number). Idempotent and order-insensitive: existing
    /// links are preserved rather than churned, so EF does not delete and reinsert rows on every save.
    /// </summary>
    public void SetTags(IEnumerable<Guid> tagIds)
    {
        var target = tagIds.Distinct().ToHashSet();

        _tags.RemoveAll(t => !target.Contains(t.TagId));

        foreach (var tagId in target.Where(id => _tags.All(t => t.TagId != id)))
            _tags.Add(new ArticleTag(Guid.NewGuid(), Id, tagId));
    }

    protected override IReadOnlyList<ContentVersion> VersionsCore => _versions;

    protected override void AppendVersion(int version, string title, string summary, ContentDocument blocks)
        => _versions.Add(new ArticleVersion(Guid.NewGuid(), Id, version, title, summary, blocks));

    // The engine raises no events of its own: it does not know what it is. An article announces
    // itself, and a lesson body announces something different (ADR-0012).
    protected override void OnPublished()
        => Raise(new ArticlePublishedDomainEvent(Id, Slug.Value, CurrentVersion));

    protected override void OnUnpublished()
        => Raise(new ArticleUnpublishedDomainEvent(Id, Slug.Value));
}
