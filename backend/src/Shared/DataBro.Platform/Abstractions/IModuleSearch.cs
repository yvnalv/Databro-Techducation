namespace DataBro.Platform.Abstractions;

/// <summary>One result, in the shape every kind of searchable thing shares.</summary>
/// <param name="Path">
/// Site-relative and locale-agnostic — <c>/courses/rag</c>, <c>/articles/chunking</c>. The module
/// that owns the content owns its URL shape, so the composing layer never has to know one kind's
/// routing from another's.
/// </param>
public sealed record SearchHit(
    Guid Id,
    string Slug,
    string Path,
    string Title,
    string Summary);

/// <summary>
/// Results from one module, kept separate from every other module's (ADR-0014).
/// </summary>
/// <param name="MatchMode">
/// <c>exact</c> or <c>fuzzy</c>, per segment. Two modules can legitimately disagree — an exact hit
/// among courses alongside a typo-corrected one among articles — and flattening that to a single
/// flag would misreport one of them.
/// </param>
public sealed record SearchSegment(
    string Kind,
    IReadOnlyList<SearchHit> Hits,
    int Total,
    string MatchMode);

/// <summary>
/// A module's own search over its own content (ADR-0014).
///
/// <para>
/// Each module indexes and queries what it owns; the API host asks every registered implementation
/// and returns the segments side by side. No module reads another's tables, and nothing tries to
/// merge the results into one ranking.
/// </para>
/// <para>
/// <b>Deliberately not blended.</b> Relevance scores from two corpora are not comparable — they come
/// from different term statistics — so any single ordering across them is a fabricated number
/// wearing the costume of relevance. Segmenting is what makes this design honest, not merely cheap.
/// </para>
/// </summary>
public interface IModuleSearch
{
    /// <summary>
    /// Segment name, used as the response key and to order the segments. Stable: the site's UI keys
    /// its section headings off it.
    /// </summary>
    string Kind { get; }

    /// <summary>Lower sorts first. Courses before articles: a course is the larger commitment.</summary>
    int Order { get; }

    Task<SearchSegment> SearchAsync(
        string query,
        string locale,
        int limit,
        CancellationToken ct = default);
}
