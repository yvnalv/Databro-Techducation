using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Domain;

/// <summary>
/// The content engine: a renderable body composed of typed blocks, versioned as draft and published
/// snapshots (docs/CONTENT_MODEL.md, ADR-0004, ADR-0007, ADR-0012).
///
/// <para>
/// This is the "build the hard part once" of ADR-0007 made concrete. Everything true of *any*
/// versioned, publishable body lives here — the block pair, the published snapshot, version history,
/// and the draft → scheduled → published state machine. What differs between an
/// <see cref="Article"/> and a lesson body is context, and context lives on the derived type.
/// </para>
/// <para>
/// Each concrete type maps to its <b>own table</b> (ADR-0012, option B). Not a single table with a
/// discriminator: that would put lessons one forgotten predicate away from appearing in article
/// listings, the sitemap and the RSS feed — the same shape as the CT-6 leak. Here it cannot happen,
/// because a lesson is not in the articles table at all.
/// </para>
/// </summary>
public abstract class ContentUnit : AggregateRoot
{
    public Slug Slug { get; protected set; } = null!;
    public string Title { get; protected set; } = string.Empty;
    public string Summary { get; protected set; } = string.Empty;
    public ArticleStatus Status { get; protected set; }

    public ContentDocument DraftBlocks { get; protected set; } = ContentDocument.Empty;
    public ContentDocument? PublishedBlocks { get; protected set; }

    /// <summary>
    /// The title as last published. Null until first publish.
    ///
    /// Separate from <see cref="Title"/> for exactly the reason <see cref="PublishedBlocks"/> is
    /// separate from <see cref="DraftBlocks"/> (CT-6). Without it, editing a published unit's draft
    /// title changed the live page, the listings, the sitemap and the search index immediately — a
    /// half-written headline going public the moment it was typed.
    /// </summary>
    public string? PublishedTitle { get; protected set; }

    /// <summary>The summary as last published. See <see cref="PublishedTitle"/>.</summary>
    public string? PublishedSummary { get; protected set; }

    /// <summary>
    /// Plain-text projection of <see cref="PublishedBlocks"/>, feeding the generated search vector
    /// (ADR-0010). Written only on publish: search returns published content, so indexing a draft
    /// would make unpublished text findable.
    /// </summary>
    public string SearchText { get; protected set; } = string.Empty;

    public int CurrentVersion { get; protected set; }
    public int ReadingTimeMinutes { get; protected set; }

    public DateTimeOffset? PublishedAt { get; protected set; }
    public DateTimeOffset? ScheduledFor { get; protected set; }

    /// <summary>
    /// Published history, newest version last (CT-8).
    ///
    /// Delegated to the derived type rather than held here: each unit type stores its versions in
    /// its own table, so each owns a differently-typed list and EF maps each relationship to the
    /// right table. The engine only ever needs to read them and append one.
    /// </summary>
    public IReadOnlyList<ContentVersion> Versions => VersionsCore;

    protected abstract IReadOnlyList<ContentVersion> VersionsCore { get; }

    /// <summary>Appends a snapshot of the concrete type's own version class.</summary>
    protected abstract void AppendVersion(int version, string title, string summary, ContentDocument blocks);

    protected ContentUnit() { } // EF

    /// <summary>
    /// Initialises the engine's state for a new draft. Called by each derived type's factory, which
    /// owns its own context fields — a base factory would have to know about authors and categories.
    /// </summary>
    protected void InitialiseDraft(Guid id, Slug slug, string title, string summary, ContentDocument blocks)
    {
        Id = id;
        Slug = slug;
        Title = title.Trim();
        Summary = summary.Trim();
        DraftBlocks = blocks;
        Status = ArticleStatus.Draft;
        CurrentVersion = 0;
        ReadingTimeMinutes = blocks.EstimateReadingTimeMinutes();
    }

    /// <summary>Updates the mutable draft. Slug is intentionally not editable here once published (CT-2).</summary>
    public void UpdateDraft(string title, string summary, ContentDocument blocks)
    {
        Title = title.Trim();
        Summary = summary.Trim();
        DraftBlocks = blocks;
        ReadingTimeMinutes = blocks.EstimateReadingTimeMinutes();
    }

    /// <summary>
    /// Changes the slug and returns the previous one, or null when the slug is unchanged. The public
    /// URL is a promise: once published, the caller must record a 301 from the old path (CT-2/CT-3) —
    /// a decision the service makes from <see cref="PublishedAt"/>, since a never-published draft has
    /// no indexed URL to protect.
    /// </summary>
    public Slug? ChangeSlug(Slug newSlug)
    {
        if (Slug.Equals(newSlug)) return null;

        var previous = Slug;
        Slug = newSlug;
        return previous;
    }

    /// <summary>
    /// Schedules an automatic publish at <paramref name="scheduledFor"/> (rule CT-7). Enforces the
    /// publish preconditions now so a schedule cannot be set on something that can never publish, and
    /// requires a future time. Rescheduling a still-scheduled unit is allowed; a currently-published
    /// one must be unpublished first.
    /// </summary>
    public Result Schedule(DateTimeOffset scheduledFor, DateTimeOffset now)
    {
        if (Status == ArticleStatus.Published)
            return Result.Failure(Error.Conflict("Published content cannot be scheduled; unpublish it first."));

        var ready = CanPublish();
        if (ready.IsFailure) return ready;

        if (scheduledFor <= now)
            return Result.Failure(Error.Rule("The scheduled time must be in the future."));

        Status = ArticleStatus.Scheduled;
        ScheduledFor = scheduledFor;
        return Result.Success();
    }

    /// <summary>
    /// Cancels a pending schedule and returns to draft (CT-7).
    ///
    /// Without this, scheduling is a one-way door: <see cref="Unpublish"/> only accepts a
    /// <c>Published</c> unit, so an editor who scheduled something and changed their mind had no way
    /// back. Deliberately leaves the draft untouched — cancelling is a decision about *when*, not
    /// about *what*.
    /// </summary>
    public Result CancelSchedule()
    {
        if (Status != ArticleStatus.Scheduled)
            return Result.Failure(Error.Conflict("Only scheduled content can have its schedule cancelled."));

        Status = ArticleStatus.Draft;
        ScheduledFor = null;
        return Result.Success();
    }

    /// <summary>
    /// Copies a past version into the draft (CT-8).
    ///
    /// It <b>never mutates history</b>: version rows are append-only and the published copy is
    /// untouched. Restoring loads old content into the draft, and publishing it afterwards writes a
    /// *new* version — so a restore is itself recorded rather than rewriting the past. That is why
    /// this is a draft operation behind <c>Content.Edit</c> and not a publishing act.
    /// </summary>
    public Result RestoreVersion(int version)
    {
        var snapshot = VersionsCore.FirstOrDefault(v => v.Version == version);
        if (snapshot is null)
            return Result.Failure(Error.NotFound($"Version {version} does not exist for this content."));

        Title = snapshot.Title;
        Summary = snapshot.Summary;
        DraftBlocks = snapshot.Blocks;
        ReadingTimeMinutes = snapshot.Blocks.EstimateReadingTimeMinutes();

        return Result.Success();
    }

    /// <summary>
    /// Publishes: snapshots the draft into the published copy, writes an immutable version row, and
    /// increments the version — atomically (rules CT-1, CT-5, CT-6, CT-8).
    /// </summary>
    public Result Publish(DateTimeOffset now)
    {
        var ready = CanPublish();
        if (ready.IsFailure) return ready;

        CurrentVersion += 1;
        PublishedBlocks = DraftBlocks;
        // Snapshotted alongside the blocks, not left pointing at the mutable draft fields — that is
        // what keeps an in-progress headline off the live page (CT-6).
        PublishedTitle = Title;
        PublishedSummary = Summary;
        SearchText = DraftBlocks.ToPlainText();
        Status = ArticleStatus.Published;
        PublishedAt = now;
        ScheduledFor = null;

        AppendVersion(CurrentVersion, Title, Summary, DraftBlocks);

        OnPublished();
        return Result.Success();
    }

    public Result Unpublish()
    {
        if (Status != ArticleStatus.Published)
            return Result.Failure(Error.Conflict("Only published content can be unpublished."));

        Status = ArticleStatus.Unpublished;
        OnUnpublished();
        return Result.Success();
    }

    /// <summary>
    /// Recomputes <see cref="SearchText"/> from the current published blocks, without touching
    /// version history or timestamps. For backfilling content published before the projection
    /// existed — publishing is what maintains it from here on.
    /// </summary>
    public void RebuildSearchText() => SearchText = PublishedBlocks?.ToPlainText() ?? string.Empty;

    /// <summary>The preconditions every content unit must meet before it can go live.</summary>
    private Result CanPublish()
    {
        if (string.IsNullOrWhiteSpace(Title))
            return Result.Failure(Error.Rule("Content requires a title before it can be published."));

        if (!DraftBlocks.HasContent)
            return Result.Failure(Error.Rule("Content requires at least one block before it can be published."));

        return Result.Success();
    }

    /// <summary>
    /// Raises the derived type's own publish event.
    ///
    /// A hook rather than an event raised by the base, because the base does not know what it is: an
    /// <c>ArticlePublished</c> event emitted for a lesson would be a lie, and once the outbox exists
    /// it would be a lie delivered to every subscriber.
    /// </summary>
    protected abstract void OnPublished();

    protected abstract void OnUnpublished();
}
