using System.Text.Json.Nodes;

namespace DataBro.Platform.Abstractions;

/// <summary>
/// One typed block of a content body, as another module is allowed to see it. Mirrors the stored
/// JSONB shape (docs/CONTENT_MODEL.md §2): a stable id, a type, and a free-form data object.
/// </summary>
public sealed record ContentBlockView(string Id, string Type, JsonObject? Data);

/// <summary>
/// A lesson's renderable body as the Learning module sees it.
/// </summary>
/// <param name="Blocks">
/// The <b>published</b> snapshot, never the draft — empty when the body has not been published.
/// This is CT-6 at the module boundary: if the draft were readable here, a half-written lesson would
/// reach a learner the moment it was typed, exactly as a draft title once reached the public article
/// page.
/// </param>
/// <param name="PublishedAt">
/// Null when the body has never been published. Learning needs this to tell "no body yet" from "a
/// body that is deliberately empty", and to warn an author before a course goes live around it.
/// </param>
public sealed record LessonContentView(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    int ReadingTimeMinutes,
    int CurrentVersion,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<ContentBlockView> Blocks);

/// <summary>
/// Read-only cross-module access to lesson bodies, owned by Content and consumed by Learning
/// (ADR-0008, ADR-0012).
///
/// <para>
/// Named for lesson bodies rather than for content units generally, which is a deliberate narrowing
/// of the name ADR-0012 used provisionally. A reader that resolved *any* content unit would resolve
/// an article id too — letting Learning attach an article as a lesson and quietly undoing the
/// separation the whole ADR exists to enforce. It reads one table because it should only ever read
/// one table.
/// </para>
/// <para>
/// Batch-shaped, and here it matters more than for authors or media: rendering a course module means
/// resolving every lesson in it at once, and a per-item interface would be an N+1 on a learner's
/// hottest path.
/// </para>
/// </summary>
public interface ILessonContentReader
{
    /// <summary>
    /// Resolves the given lesson-body ids. Ids with no matching body are absent from the result, so
    /// callers must tolerate a partial map — a body deleted out from under a lesson must leave the
    /// course renderable rather than throwing.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, LessonContentView>> GetLessonContentAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default);
}
