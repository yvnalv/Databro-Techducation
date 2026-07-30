using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Domain;

/// <summary>
/// The Article aggregate — a content unit composed of typed blocks, versioned as draft and published
/// snapshots (docs/CONTENT_MODEL.md, ADR-0004, ADR-0007). Public consumers only ever see
/// <see cref="PublishedBlocks"/>.
/// </summary>
public sealed class Article : AggregateRoot
{
    private readonly List<ArticleVersion> _versions = [];

    public Slug Slug { get; private set; } = null!;
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public ArticleStatus Status { get; private set; }
    public Visibility Visibility { get; private set; }
    public string Locale { get; private set; } = "en";
    public Guid? TranslationGroupId { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid? CategoryId { get; private set; }

    public ContentDocument DraftBlocks { get; private set; } = ContentDocument.Empty;
    public ContentDocument? PublishedBlocks { get; private set; }
    public int CurrentVersion { get; private set; }
    public int ReadingTimeMinutes { get; private set; }
    public SeoMetadata Seo { get; private set; } = SeoMetadata.Default;

    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? ScheduledFor { get; private set; }

    public IReadOnlyList<ArticleVersion> Versions => _versions.AsReadOnly();

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
        return new Article
        {
            Id = id,
            Slug = slug,
            Title = title.Trim(),
            Summary = summary.Trim(),
            AuthorId = authorId,
            DraftBlocks = blocks,
            Locale = locale,
            Visibility = visibility,
            Status = ArticleStatus.Draft,
            CurrentVersion = 0,
            ReadingTimeMinutes = blocks.EstimateReadingTimeMinutes(),
            Seo = seo ?? SeoMetadata.Default,
        };
    }

    /// <summary>Updates the mutable draft. Slug is intentionally not editable here once published (CT-2).</summary>
    public void UpdateDraft(string title, string summary, ContentDocument blocks, SeoMetadata? seo = null)
    {
        Title = title.Trim();
        Summary = summary.Trim();
        DraftBlocks = blocks;
        ReadingTimeMinutes = blocks.EstimateReadingTimeMinutes();
        if (seo is not null) Seo = seo;
    }

    public void SetCategory(Guid? categoryId) => CategoryId = categoryId;

    /// <summary>
    /// Publishes the article: snapshots the draft into the published copy, writes an immutable
    /// version row, and increments the version — atomically (rules CT-1, CT-5, CT-6, CT-8).
    /// </summary>
    public Result Publish(DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(Title))
            return Result.Failure(Error.Rule("An article requires a title before it can be published."));

        if (!DraftBlocks.HasContent)
            return Result.Failure(Error.Rule("An article requires at least one content block before it can be published."));

        CurrentVersion += 1;
        PublishedBlocks = DraftBlocks;
        Status = ArticleStatus.Published;
        PublishedAt = now;
        ScheduledFor = null;

        _versions.Add(new ArticleVersion(
            Guid.NewGuid(), Id, CurrentVersion, Title, Summary, DraftBlocks));

        Raise(new ArticlePublishedDomainEvent(Id, Slug.Value, CurrentVersion));
        return Result.Success();
    }

    public Result Unpublish()
    {
        if (Status != ArticleStatus.Published)
            return Result.Failure(Error.Conflict("Only a published article can be unpublished."));

        Status = ArticleStatus.Unpublished;
        Raise(new ArticleUnpublishedDomainEvent(Id, Slug.Value));
        return Result.Success();
    }
}
