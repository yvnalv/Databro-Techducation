using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Learning.Domain;

/// <summary>
/// A named section within a course — "Retrieval", "Evaluation" — holding an ordered run of lessons.
///
/// Owned by <see cref="Course"/>; every mutation here is reached through the course so a whole
/// rearrangement saves as one transaction (ADR-0013).
/// </summary>
public sealed class CourseModule : Entity
{
    private readonly List<Lesson> _lessons = [];

    public Guid CourseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;

    /// <summary>Position within the course. Contiguous from zero; the course owns this.</summary>
    public int Order { get; private set; }

    public IReadOnlyList<Lesson> Lessons => _lessons.OrderBy(l => l.Order).ToList();

    private CourseModule() { } // EF

    internal CourseModule(Guid id, Guid courseId, string title, int order)
        : base(id)
    {
        CourseId = courseId;
        Title = title.Trim();
        Order = order;
    }

    internal void SetOrder(int order) => Order = order;

    public void Rename(string title, string? summary = null)
    {
        Title = title.Trim();
        if (summary is not null) Summary = summary.Trim();
    }

    /// <summary>
    /// Appends a lesson for the given content body. Refuses a body already used elsewhere in this
    /// module: the same lesson twice in one section is always a mistake, and it would make
    /// "completed" ambiguous once progress exists.
    /// </summary>
    public Result<Lesson> AddLesson(Guid lessonId, Guid contentUnitId)
    {
        if (_lessons.Any(l => l.ContentUnitId == contentUnitId))
            return Result.Failure<Lesson>(Error.Conflict("That lesson is already in this module."));

        var lesson = new Lesson(lessonId, Id, contentUnitId, _lessons.Count);
        _lessons.Add(lesson);
        Normalise();

        return Result.Success(lesson);
    }

    public Result RemoveLesson(Guid lessonId)
    {
        var lesson = _lessons.FirstOrDefault(l => l.Id == lessonId);
        if (lesson is null)
            return Result.Failure(Error.NotFound("Lesson not found in this module."));

        _lessons.Remove(lesson);
        Normalise();

        // Leaving a dangling prerequisite would let a lesson require something no longer in the
        // curriculum, which nothing downstream could resolve.
        foreach (var remaining in _lessons)
            remaining.RequirePrerequisites(remaining.PrerequisiteLessonIds.Where(id => id != lessonId));

        return Result.Success();
    }

    /// <summary>
    /// Reorders the lessons to match <paramref name="orderedLessonIds"/>. Ids not named keep their
    /// relative order after the ones that are, so a partial list from a UI cannot silently drop
    /// anything.
    /// </summary>
    public Result ReorderLessons(IReadOnlyList<Guid> orderedLessonIds)
    {
        if (orderedLessonIds.Distinct().Count() != orderedLessonIds.Count)
            return Result.Failure(Error.Validation("The same lesson was listed more than once."));

        if (orderedLessonIds.Any(id => _lessons.All(l => l.Id != id)))
            return Result.Failure(Error.Validation("The order refers to a lesson that is not in this module."));

        var ranked = orderedLessonIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        var sorted = _lessons
            .OrderBy(l => ranked.TryGetValue(l.Id, out var rank) ? rank : int.MaxValue)
            .ThenBy(l => l.Order)
            .ToList();

        // Write the new sequence into Order before normalising: Normalise now sorts by Order, so the
        // arrangement has to live there rather than in the backing list's transient position.
        for (var i = 0; i < sorted.Count; i++)
            sorted[i].SetOrder(i);
        Normalise();

        return Result.Success();
    }

    /// <summary>
    /// Rewrites positions to <c>0..n-1</c> in current list order (ADR-0013). Called after every
    /// structural change, so no caller can leave a gap or a duplicate behind.
    /// </summary>
    private void Normalise()
    {
        // Renumber by each lesson's own Order, not by backing-list position: the aggregate is
        // reloaded on every authoring call and the EF include does not order this collection, so
        // renumbering by raw position let a later structural change reshuffle a saved section. See the
        // fuller note on Course.NormaliseModules — the two share the invariant and the hazard.
        var ordered = _lessons.OrderBy(l => l.Order).ToList();
        _lessons.Clear();
        _lessons.AddRange(ordered);
        for (var i = 0; i < _lessons.Count; i++)
            _lessons[i].SetOrder(i);
    }
}
