using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Results;

namespace DataBro.Modules.Learning.Application;

/// <summary>
/// A learner's saved list.
///
/// <para>
/// Takes the learner's id explicitly rather than reading an ambient <c>ICurrentUser</c>, the same as
/// <see cref="EnrollmentService"/> and for the same reason: on a surface where the id <i>is</i> the
/// authorization boundary, an implicit parameter is the one that gets forgotten in a branch and
/// reads someone else's list.
/// </para>
/// </summary>
public sealed class BookmarkService(
    IBookmarkRepository bookmarks,
    ICourseRepository courses,
    ILessonContentReader bodies,
    IClock clock)
{
    /// <summary>
    /// Saves something, or returns what is already saved.
    ///
    /// <para>
    /// <b>Idempotent</b>, like enrolling (LN-9): a second save is a double-tapped bookmark button,
    /// and a 409 would make every client handle a failure that means "it worked".
    /// </para>
    /// </summary>
    public async Task<Result<BookmarkDto>> SaveAsync(
        Guid userId, CreateBookmarkRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<BookmarkKind>(request.Kind, ignoreCase: true, out var kind))
            return Result.Failure<BookmarkDto>(Error.Validation("Unknown bookmark kind."));

        // Checked before saving: a bookmark pointing at nothing is a row that can only ever render
        // as "unavailable", and it is cheaper to refuse it than to explain it later.
        var resolved = await ResolveAsync(kind, request.TargetId, ct);
        if (resolved is null)
            return Result.Failure<BookmarkDto>(Error.NotFound("That is not something you can save."));

        var existing = await bookmarks.FindAsync(userId, kind, request.TargetId, ct);
        if (existing is not null) return Result.Success(Compose(existing, resolved));

        var bookmark = Bookmark.Create(Guid.NewGuid(), userId, kind, request.TargetId, clock.UtcNow);

        await bookmarks.AddAsync(bookmark, ct);
        await bookmarks.SaveChangesAsync(ct);

        return Result.Success(Compose(bookmark, resolved));
    }

    /// <summary>
    /// Removes a saved item. Succeeds when it was not saved — un-saving must never fail, for the
    /// same reason signing out must not: a client that cannot complete it leaves the UI lying.
    /// </summary>
    public async Task<Result> RemoveAsync(
        Guid userId, string kindText, Guid targetId, CancellationToken ct = default)
    {
        if (!Enum.TryParse<BookmarkKind>(kindText, ignoreCase: true, out var kind))
            return Result.Failure(Error.Validation("Unknown bookmark kind."));

        var existing = await bookmarks.FindAsync(userId, kind, targetId, ct);
        if (existing is null) return Result.Success();

        bookmarks.Remove(existing);
        await bookmarks.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>
    /// The saved list, resolved for display.
    ///
    /// <para>
    /// Targets are resolved one page at a time and <b>a target that no longer resolves is kept</b>,
    /// with a null path. Silently dropping it would make a learner's list shrink without explanation
    /// when an author unpublishes something; showing it as unavailable at least says what happened.
    /// </para>
    /// </summary>
    public async Task<PagedResult<BookmarkDto>> ListAsync(
        Guid userId, PageRequest page, CancellationToken ct = default)
    {
        var result = await bookmarks.ListForUserAsync(userId, page, ct);
        if (result.Items.Count == 0)
            return new PagedResult<BookmarkDto>([], result.Page, result.PageSize, result.Total);

        var items = new List<BookmarkDto>();
        foreach (var bookmark in result.Items)
            items.Add(Compose(bookmark, await ResolveAsync(bookmark.Kind, bookmark.TargetId, ct)));

        return new PagedResult<BookmarkDto>(items, result.Page, result.PageSize, result.Total);
    }

    /// <summary>Which of a learner's saves are among the given targets — what a list of cards needs.</summary>
    public async Task<IReadOnlyList<Guid>> SavedTargetsAsync(
        Guid userId, CancellationToken ct = default)
    {
        var all = await bookmarks.ListForUserAsync(userId, new PageRequest(1, 200), ct);
        return all.Items.Select(b => b.TargetId).ToList();
    }

    /// <summary>Title and public path for a target, or null when it no longer resolves.</summary>
    private async Task<(string Title, string? Path)?> ResolveAsync(
        BookmarkKind kind, Guid targetId, CancellationToken ct)
    {
        if (kind == BookmarkKind.Course)
        {
            var course = await courses.GetByIdAsync(targetId, ct);
            if (course is null) return null;

            // An unpublished course keeps its title and loses its link: the learner saved something
            // real, and it may come back.
            return (course.Title,
                course.Status == CourseStatus.Published ? $"/courses/{course.Slug.Value}" : null);
        }

        // A lesson is only reachable through a course, so resolving one means finding which course
        // holds it. Not free, but a saved list is a page a learner opens occasionally, not a hot path.
        var all = await courses.ListAllAsync(new PageRequest(1, 200), ct);

        foreach (var course in all.Items)
        {
            var lesson = course.Modules.SelectMany(m => m.Lessons).FirstOrDefault(l => l.Id == targetId);
            if (lesson is null) continue;

            var resolved = await bodies.GetLessonContentAsync([lesson.ContentUnitId], ct);
            if (!resolved.TryGetValue(lesson.ContentUnitId, out var body)) return null;

            var reachable = course.Status == CourseStatus.Published && body.PublishedAt is not null;

            return (body.Title,
                reachable ? $"/courses/{course.Slug.Value}/{body.Slug}" : null);
        }

        return null;
    }

    private static BookmarkDto Compose(Bookmark bookmark, (string Title, string? Path)? resolved) =>
        new(bookmark.Id,
            bookmark.Kind.ToWire(),
            bookmark.TargetId,
            resolved?.Title ?? string.Empty,
            resolved?.Path,
            bookmark.SavedAt);
}
