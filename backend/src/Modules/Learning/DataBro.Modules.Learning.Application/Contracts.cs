using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Abstractions;

namespace DataBro.Modules.Learning.Application;

// DTOs exchanged with the API layer. Enums cross the wire lowercase, matching Content's contracts
// and the TypeScript unions in @databro/types.

/// <summary>
/// A lesson as a learner sees it: curriculum metadata joined to the body Content resolved.
/// </summary>
/// <param name="Blocks">
/// Empty when the body is not published. The public read never returns such a lesson at all — this
/// shape is also used by the authoring view, where an author must see the gap.
/// </param>
public sealed record LessonDto(
    Guid Id,
    Guid ContentUnitId,
    string Slug,
    string Title,
    string Summary,
    int Order,
    int EstimatedMinutes,
    string Difficulty,
    IReadOnlyList<string> Objectives,
    IReadOnlyList<Guid> PrerequisiteLessonIds,
    bool IsPublished,
    IReadOnlyList<ContentBlockView> Blocks);

public sealed record CourseModuleDto(
    Guid Id,
    string Title,
    string Summary,
    int Order,
    IReadOnlyList<LessonDto> Lessons);

public sealed record CourseDto(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string Status,
    string Difficulty,
    int LessonCount,
    int EstimatedMinutes,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<CourseModuleDto> Modules);

/// <summary>A course as a card in a listing — no curriculum, because a card does not render one.</summary>
public sealed record CourseSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string Status,
    string Difficulty,
    int LessonCount,
    int EstimatedMinutes,
    DateTimeOffset? PublishedAt);

public sealed record LearningPathDto(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string Status,
    string Difficulty,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<CourseSummaryDto> Courses);

// ---- Requests ----

public sealed record CreateCourseRequest(string Title, string Summary, string? Slug = null, string? Difficulty = null);

public sealed record UpdateCourseRequest(string Title, string Summary, string? Difficulty = null);

public sealed record AddModuleRequest(string Title);

public sealed record UpdateModuleRequest(string Title, string? Summary = null);

public sealed record AddLessonRequest(Guid ContentUnitId);

public sealed record UpdateLessonRequest(
    int EstimatedMinutes,
    string? Difficulty = null,
    IReadOnlyList<string>? Objectives = null,
    IReadOnlyList<Guid>? PrerequisiteLessonIds = null);

public sealed record ReorderRequest(IReadOnlyList<Guid> OrderedIds);

public sealed record CreateLearningPathRequest(string Title, string Summary, string? Slug = null, string? Difficulty = null);

public sealed record UpdateLearningPathRequest(string Title, string Summary, string? Difficulty = null);

/// <summary>A neighbouring lesson, as a prev/next link needs it. No body — it is a link.</summary>
public sealed record LessonLinkDto(Guid Id, string Slug, string Title);

/// <summary>One rendered size of an image, mirroring <see cref="MediaVariantSummary"/> for the wire.</summary>
public sealed record MediaVariantRefDto(string Name, string Url, int Width, int Height);

/// <summary>
/// An image a lesson block references, resolved to something an <c>&lt;img&gt;</c> can render.
///
/// <para>
/// Fetched through <see cref="IMediaDirectory"/> (ADR-0008), the same contract Content uses for
/// article images — a lesson and an article are one content primitive, so a lesson body must resolve
/// its media the same way. Shaped identically to Content's own <c>MediaRefDto</c> rather than shared:
/// the two live in different modules and must not depend on one another.
/// </para>
/// </summary>
public sealed record MediaRefDto(
    string Url,
    string AltText,
    int Width,
    int Height,
    IReadOnlyList<MediaVariantRefDto> Variants);

/// <summary>
/// One lesson as its own page: the body, plus enough of the curriculum around it to navigate.
///
/// <para>
/// A distinct read from <see cref="CourseDto"/> rather than a client-side pick out of it. The course
/// page carries every lesson body it has, which is right for one request that renders a whole
/// curriculum and wrong as the cost of reading lesson three of fifty. This ships one body and two
/// links.
/// </para>
/// <para>
/// Neighbours are resolved across module boundaries, not within one: the last lesson of a module
/// links to the first of the next. A learner moving through a course experiences one sequence, and
/// stopping them at a section break would be the data model showing through.
/// </para>
/// </summary>
public sealed record LessonPageDto(
    Guid CourseId,
    string CourseSlug,
    string CourseTitle,
    string ModuleTitle,
    /// <summary>Position in the whole course, 1-based — "Lesson 4 of 12", not "lesson 0 of module 2".</summary>
    int Position,
    int TotalLessons,
    LessonDto Lesson,
    LessonLinkDto? Previous,
    LessonLinkDto? Next,
    /// <summary>
    /// Every media id the lesson body references, resolved to a renderable ref and keyed by id as a
    /// string — the shape the renderer's media resolver expects. Only the lesson on this page is
    /// resolved, not its neighbours: prev/next are links, and a link renders no images. An id absent
    /// from the map is one whose asset is gone; the renderer shows a placeholder, not a broken image.
    /// </summary>
    IReadOnlyDictionary<string, MediaRefDto> Media);

/// <summary>
/// A learner's progress in one course — the shape behind both a dashboard card and a course page's
/// "you are here" banner.
/// </summary>
/// <param name="TotalLessons">
/// Published lessons <i>now</i>. Moves when the curriculum grows, which is why
/// <paramref name="PercentComplete"/> is derived rather than stored.
/// </param>
/// <param name="CompletedAt">
/// Set once and never cleared. A learner can legitimately be complete with
/// <paramref name="CompletedLessons"/> below <paramref name="TotalLessons"/> — the course grew after
/// they finished it (LN-6).
/// </param>
public sealed record EnrollmentDto(
    Guid Id,
    Guid CourseId,
    string CourseSlug,
    string CourseTitle,
    DateTimeOffset EnrolledAt,
    DateTimeOffset? CompletedAt,
    Guid? LastLessonId,
    /// <summary>The resume point as a URL segment. Null when that lesson is no longer reachable.</summary>
    string? LastLessonSlug,
    DateTimeOffset? LastAccessedAt,
    int TotalLessons,
    int CompletedLessons,
    int PercentComplete,
    IReadOnlyList<Guid> CompletedLessonIds);

/// <summary>
/// A saved item, resolved for display.
/// </summary>
/// <param name="Title">
/// Read live from the course or lesson, never stored on the bookmark — a saved list that disagreed
/// with the thing it points at would defeat the point of saving it.
/// </param>
/// <param name="Path">
/// Where it lives on the public site, or null when the target has been unpublished or removed. A
/// null path is an ordinary state and the UI shows the row as unavailable rather than linking into a
/// 404.
/// </param>
public sealed record BookmarkDto(
    Guid Id,
    string Kind,
    Guid TargetId,
    string Title,
    string? Path,
    DateTimeOffset SavedAt);

public sealed record CreateBookmarkRequest(string Kind, Guid TargetId);

/// <summary>
/// A learner's study streak.
/// </summary>
/// <param name="Current">
/// Days in a row <b>as of today</b>, not the raw stored counter — a streak decays with time rather
/// than with writes, so someone who last studied three days ago reads as 0 here even though nothing
/// has written to their row since.
/// </param>
/// <param name="ActiveToday">
/// Whether today already counts. What lets a UI say "done today" instead of nagging someone who has
/// already studied.
/// </param>
public sealed record StreakDto(
    int Current,
    int Longest,
    DateOnly? LastActiveOn,
    bool ActiveToday);

internal static class LearningMapping
{
    public static string ToWire<TEnum>(this TEnum value) where TEnum : struct, Enum =>
        value.ToString().ToLowerInvariant();

    public static Difficulty ParseDifficulty(string? value) =>
        Enum.TryParse<Difficulty>(value, ignoreCase: true, out var parsed) ? parsed : Difficulty.Beginner;

    public static CourseSummaryDto ToSummaryDto(this Course c) =>
        new(c.Id, c.Slug.Value, c.Title, c.Summary, c.Status.ToWire(), c.Difficulty.ToWire(),
            c.LessonCount, c.EstimatedMinutes, c.PublishedAt);
}
