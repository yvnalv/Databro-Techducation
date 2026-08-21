using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Learning.Domain;

/// <summary>
/// A curated sequence of courses — "Become an LLM Engineer" — pointing at courses it does not own.
///
/// <para>
/// A separate aggregate root from <see cref="Course"/>, holding an <b>ordered list of course ids</b>
/// rather than the courses themselves (ADR-0013). A course belongs to any number of paths: an
/// introductory Python course legitimately opens several tracks. Owning them here would put the same
/// course inside two aggregates, and then "which one wins" has no good answer.
/// </para>
/// </summary>
public sealed class LearningPath : AggregateRoot
{
    private readonly List<PathCourse> _courses = [];

    public Slug Slug { get; private set; } = null!;
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public CourseStatus Status { get; private set; }
    public Difficulty Difficulty { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>Course ids in the order a learner should take them.</summary>
    public IReadOnlyList<Guid> CourseIds => _courses.OrderBy(c => c.Order).Select(c => c.CourseId).ToList();

    private LearningPath() { } // EF

    public static LearningPath CreateDraft(Guid id, Slug slug, string title, string summary,
        Difficulty difficulty = Difficulty.Beginner) =>
        new()
        {
            Id = id,
            Slug = slug,
            Title = title.Trim(),
            Summary = summary.Trim(),
            Difficulty = difficulty,
            Status = CourseStatus.Draft,
        };

    public void Describe(string title, string summary, Difficulty difficulty)
    {
        Title = title.Trim();
        Summary = summary.Trim();
        Difficulty = difficulty;
    }

    public Slug? ChangeSlug(Slug newSlug)
    {
        if (Slug.Equals(newSlug)) return null;

        var previous = Slug;
        Slug = newSlug;
        return previous;
    }

    /// <summary>
    /// Appends a course. Idempotent: adding one already in the path is a no-op rather than an error,
    /// because a builder UI dropping the same card twice is a slip, not a decision worth refusing.
    /// </summary>
    public void AddCourse(Guid courseId)
    {
        if (_courses.Any(c => c.CourseId == courseId)) return;

        _courses.Add(new PathCourse(Guid.NewGuid(), Id, courseId, _courses.Count));
        Normalise();
    }

    public void RemoveCourse(Guid courseId)
    {
        _courses.RemoveAll(c => c.CourseId == courseId);
        Normalise();
    }

    /// <summary>Reorders to match <paramref name="orderedCourseIds"/>; unnamed ids keep their relative order.</summary>
    public Result ReorderCourses(IReadOnlyList<Guid> orderedCourseIds)
    {
        if (orderedCourseIds.Distinct().Count() != orderedCourseIds.Count)
            return Result.Failure(Error.Validation("The same course was listed more than once."));

        if (orderedCourseIds.Any(id => _courses.All(c => c.CourseId != id)))
            return Result.Failure(Error.Validation("The order refers to a course that is not in this path."));

        var ranked = orderedCourseIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        var sorted = _courses
            .OrderBy(c => ranked.TryGetValue(c.CourseId, out var rank) ? rank : int.MaxValue)
            .ThenBy(c => c.Order)
            .ToList();

        // Write the new sequence into Order before normalising: Normalise now sorts by Order, so the
        // arrangement has to live there rather than in the backing list's transient position.
        for (var i = 0; i < sorted.Count; i++)
            sorted[i].SetOrder(i);
        Normalise();

        return Result.Success();
    }

    /// <summary>
    /// Publishes the path. Requires at least one course — an empty path is not a path — but does not
    /// require those courses to be published, for the same reason a course does not require every
    /// lesson to be (ADR-0013).
    /// </summary>
    public Result Publish(DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(Title))
            return Result.Failure(Error.Rule("A learning path requires a title before it can be published."));

        if (_courses.Count == 0)
            return Result.Failure(Error.Rule("A learning path requires at least one course before it can be published."));

        Status = CourseStatus.Published;
        PublishedAt = now;
        return Result.Success();
    }

    public Result Unpublish()
    {
        if (Status != CourseStatus.Published)
            return Result.Failure(Error.Conflict("Only a published learning path can be unpublished."));

        Status = CourseStatus.Unpublished;
        return Result.Success();
    }

    private void Normalise()
    {
        // Renumber by each entry's own Order, not by backing-list position: the aggregate is reloaded
        // on every curation call and the EF include does not order this collection, so renumbering by
        // raw position let a later AddCourse reshuffle an already-saved path. See the fuller note on
        // Course.NormaliseModules — the same invariant and the same hazard.
        var ordered = _courses.OrderBy(c => c.Order).ToList();
        _courses.Clear();
        _courses.AddRange(ordered);
        for (var i = 0; i < _courses.Count; i++)
            _courses[i].SetOrder(i);
    }
}

/// <summary>
/// A course's position in a path. A join row rather than a navigation, because the path references
/// courses it does not own.
/// </summary>
public sealed class PathCourse : Entity
{
    public Guid LearningPathId { get; private set; }
    public Guid CourseId { get; private set; }
    public int Order { get; private set; }

    private PathCourse() { } // EF

    internal PathCourse(Guid id, Guid learningPathId, Guid courseId, int order) : base(id)
    {
        LearningPathId = learningPathId;
        CourseId = courseId;
        Order = order;
    }

    internal void SetOrder(int order) => Order = order;
}
