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
    DateTimeOffset? LastAccessedAt,
    int TotalLessons,
    int CompletedLessons,
    int PercentComplete,
    IReadOnlyList<Guid> CompletedLessonIds);

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
