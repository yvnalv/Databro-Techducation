using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Learning.Domain;

/// <summary>
/// A course: an ordered curriculum of modules, each an ordered run of lessons (ADR-0013).
///
/// <para>
/// The aggregate root, and the consistency boundary is chosen for the operation that dominates
/// authoring — dragging modules and lessons around. One root means a whole rearrangement is a single
/// atomic save rather than a scatter of independent writes that can half-apply.
/// </para>
/// <para>
/// It publishes on its own schedule, independent of its lessons: a published course shows only its
/// published lessons. Requiring every lesson to be finished first would make a large curriculum
/// unpublishable until the last one was written, and courses grow after launch.
/// </para>
/// </summary>
public sealed class Course : AggregateRoot
{
    private readonly List<CourseModule> _modules = [];

    public Slug Slug { get; private set; } = null!;
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public CourseStatus Status { get; private set; }
    public Difficulty Difficulty { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public IReadOnlyList<CourseModule> Modules => _modules.OrderBy(m => m.Order).ToList();

    /// <summary>Total lessons across every module — what a course card shows.</summary>
    public int LessonCount => _modules.Sum(m => m.Lessons.Count);

    /// <summary>
    /// Summed from the lessons rather than stored, so it cannot drift from the curriculum it
    /// describes.
    /// </summary>
    public int EstimatedMinutes => _modules.Sum(m => m.Lessons.Sum(l => l.EstimatedMinutes));

    private Course() { } // EF

    public static Course CreateDraft(Guid id, Slug slug, string title, string summary,
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

    /// <summary>
    /// Changes the slug and returns the previous one, or null when unchanged. A published course's
    /// URL is a promise, so the caller records a 301 from the old path exactly as it does for an
    /// article (CT-2/CT-3).
    /// </summary>
    public Slug? ChangeSlug(Slug newSlug)
    {
        if (Slug.Equals(newSlug)) return null;

        var previous = Slug;
        Slug = newSlug;
        return previous;
    }

    // ---- Curriculum ----

    public CourseModule AddModule(Guid moduleId, string title)
    {
        var module = new CourseModule(moduleId, Id, title, _modules.Count);
        _modules.Add(module);
        NormaliseModules();

        return module;
    }

    public Result RemoveModule(Guid moduleId)
    {
        var module = _modules.FirstOrDefault(m => m.Id == moduleId);
        if (module is null)
            return Result.Failure(Error.NotFound("Module not found in this course."));

        // Deliberately allowed even when it holds lessons: removing a section of a draft curriculum
        // is ordinary editing. The lesson *bodies* are Content's and survive untouched — this drops
        // the curriculum positions, not the writing.
        _modules.Remove(module);
        NormaliseModules();

        return Result.Success();
    }

    /// <summary>
    /// Reorders modules to match <paramref name="orderedModuleIds"/>. Unnamed ids keep their
    /// relative order afterwards, so a partial list cannot silently drop a section.
    /// </summary>
    public Result ReorderModules(IReadOnlyList<Guid> orderedModuleIds)
    {
        if (orderedModuleIds.Distinct().Count() != orderedModuleIds.Count)
            return Result.Failure(Error.Validation("The same module was listed more than once."));

        if (orderedModuleIds.Any(id => _modules.All(m => m.Id != id)))
            return Result.Failure(Error.Validation("The order refers to a module that is not in this course."));

        var ranked = orderedModuleIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        var sorted = _modules
            .OrderBy(m => ranked.TryGetValue(m.Id, out var rank) ? rank : int.MaxValue)
            .ThenBy(m => m.Order)
            .ToList();

        // Write the new sequence into Order before normalising: NormaliseModules now sorts by Order,
        // so the arrangement has to live there rather than in the backing list's transient position.
        for (var i = 0; i < sorted.Count; i++)
            sorted[i].SetOrder(i);
        NormaliseModules();

        return Result.Success();
    }

    public CourseModule? FindModule(Guid moduleId) => _modules.FirstOrDefault(m => m.Id == moduleId);

    // ---- Publishing ----

    /// <summary>
    /// Publishes the course. Requires a title and at least one lesson somewhere in it — an empty
    /// curriculum is not a course — but deliberately does <b>not</b> require every lesson body to be
    /// published (ADR-0013).
    /// </summary>
    public Result Publish(DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(Title))
            return Result.Failure(Error.Rule("A course requires a title before it can be published."));

        if (LessonCount == 0)
            return Result.Failure(Error.Rule("A course requires at least one lesson before it can be published."));

        Status = CourseStatus.Published;
        PublishedAt = now;

        Raise(new CoursePublishedDomainEvent(Id, Slug.Value));
        return Result.Success();
    }

    public Result Unpublish()
    {
        if (Status != CourseStatus.Published)
            return Result.Failure(Error.Conflict("Only a published course can be unpublished."));

        Status = CourseStatus.Unpublished;
        Raise(new CourseUnpublishedDomainEvent(Id, Slug.Value));
        return Result.Success();
    }

    private void NormaliseModules()
    {
        // Renumber to a contiguous 0..n by each module's own Order, NOT by its position in the backing
        // list. The aggregate is reloaded on every authoring call and the EF include does not order
        // this collection (CourseRepository.Full), so renumbering by raw list position let a later
        // AddModule reshuffle an already-saved curriculum. Ordering by Order first makes normalisation
        // independent of how EF happened to materialise the rows; the backing list is realigned to
        // match so list order and Order never disagree.
        var ordered = _modules.OrderBy(m => m.Order).ToList();
        _modules.Clear();
        _modules.AddRange(ordered);
        for (var i = 0; i < _modules.Count; i++)
            _modules[i].SetOrder(i);
    }
}
