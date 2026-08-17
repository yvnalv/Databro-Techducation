using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Learning.Domain;

/// <summary>
/// A lesson: a position in a curriculum plus the learning metadata around a content body
/// (ADR-0007, ADR-0012, ADR-0013).
///
/// <para>
/// It holds no blocks of its own. <see cref="ContentUnitId"/> points at a lesson body owned by the
/// Content module, resolved through <c>ILessonContentReader</c> — an id across a module boundary,
/// never a navigation. That split is the whole point of ADR-0007: the block engine, versioning and
/// publishing are written once and a lesson borrows them.
/// </para>
/// <para>
/// Not an aggregate root. A lesson only makes sense inside its module inside its course, and
/// reordering is the operation an authoring UI performs constantly — so the course is the
/// consistency boundary and one save covers the whole rearrangement.
/// </para>
/// </summary>
public sealed class Lesson : Entity
{
    private readonly List<Guid> _prerequisiteLessonIds = [];
    private readonly List<string> _objectives = [];

    public Guid CourseModuleId { get; private set; }

    /// <summary>The body in Content. Resolved through a contract, never joined to.</summary>
    public Guid ContentUnitId { get; private set; }

    /// <summary>Position within its module. Contiguous from zero (ADR-0013); the module owns this.</summary>
    public int Order { get; private set; }

    /// <summary>
    /// Author-declared study time. Not derived from the body's reading time: a lesson with three
    /// paragraphs and an exercise takes far longer than three paragraphs of prose, and only the
    /// author knows that.
    /// </summary>
    public int EstimatedMinutes { get; private set; }

    public Difficulty Difficulty { get; private set; }

    /// <summary>What a learner should be able to do afterwards. Required by CLAUDE.md for every lesson.</summary>
    public IReadOnlyList<string> Objectives => _objectives.AsReadOnly();

    /// <summary>
    /// Lessons that should come first. Recorded but <b>not enforced</b> — nothing yet stops a learner
    /// starting out of order, because enforcing it needs progress, which arrives with enrollment
    /// (ADR-0013).
    /// </summary>
    public IReadOnlyList<Guid> PrerequisiteLessonIds => _prerequisiteLessonIds.AsReadOnly();

    private Lesson() { } // EF

    internal Lesson(Guid id, Guid courseModuleId, Guid contentUnitId, int order)
        : base(id)
    {
        CourseModuleId = courseModuleId;
        ContentUnitId = contentUnitId;
        Order = order;
        Difficulty = Difficulty.Beginner;
    }

    internal void SetOrder(int order) => Order = order;

    /// <summary>Replaces the learning metadata. Objectives are trimmed and blanks dropped.</summary>
    public void Describe(int estimatedMinutes, Difficulty difficulty, IEnumerable<string>? objectives = null)
    {
        EstimatedMinutes = Math.Max(0, estimatedMinutes);
        Difficulty = difficulty;

        if (objectives is null) return;

        _objectives.Clear();
        _objectives.AddRange(objectives.Select(o => o.Trim()).Where(o => o.Length > 0));
    }

    /// <summary>
    /// Replaces the prerequisite set. A lesson cannot require itself — the check exists because a
    /// builder UI that lets you drag a lesson onto its own prerequisite list is easy to write.
    /// </summary>
    public void RequirePrerequisites(IEnumerable<Guid> lessonIds)
    {
        _prerequisiteLessonIds.Clear();
        _prerequisiteLessonIds.AddRange(lessonIds.Distinct().Where(id => id != Id));
    }

    /// <summary>Points the lesson at a different body, keeping its position and metadata.</summary>
    public void UseContent(Guid contentUnitId) => ContentUnitId = contentUnitId;
}
