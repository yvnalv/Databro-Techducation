using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Learning.Domain;

/// <summary>
/// One learner's relationship with one course: that they joined it, where they are in it, and which
/// lessons they have finished.
///
/// <para>
/// <b>Its own aggregate root, deliberately not part of <see cref="Course"/>.</b> The course is the
/// authoring boundary — one save covers a whole rearrangement of the curriculum. Progress is the
/// opposite shape: many learners writing constantly to their own slice, never to each other's.
/// Folding progress into the course would make marking one lesson complete load an entire
/// curriculum, and would put every learner on the platform in contention over a single aggregate.
/// Separate roots, joined by id.
/// </para>
/// <para>
/// This is the platform's first genuinely write-heavy surface. Everything before it was read-heavy
/// and cacheable; this one is neither.
/// </para>
/// </summary>
public sealed class Enrollment : AggregateRoot
{
    private readonly List<LessonProgress> _progress = [];

    /// <summary>The learner, from Identity. An id across a module boundary, never a navigation.</summary>
    public Guid UserId { get; private set; }

    public Guid CourseId { get; private set; }

    public DateTimeOffset EnrolledAt { get; private set; }

    /// <summary>
    /// When the learner finished the course, or null. <b>Stored, not derived</b> — see
    /// <see cref="TryComplete"/> for why that distinction carries the whole rule (LN-6).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsCompleted => CompletedAt is not null;

    /// <summary>
    /// The lesson to drop the learner back into. The one they last <i>opened</i>, which is not the
    /// one they last finished: someone who stopped halfway through lesson 4 wants lesson 4, not
    /// lesson 5, and not lesson 3.
    /// </summary>
    public Guid? LastLessonId { get; private set; }

    public DateTimeOffset? LastAccessedAt { get; private set; }

    public IReadOnlyList<LessonProgress> Progress => _progress.AsReadOnly();

    public int CompletedLessonCount => _progress.Count(p => p.IsCompleted);

    private Enrollment() { } // EF

    public static Enrollment Start(Guid id, Guid userId, Guid courseId, DateTimeOffset now)
    {
        var enrollment = new Enrollment
        {
            Id = id,
            UserId = userId,
            CourseId = courseId,
            EnrolledAt = now,
        };

        enrollment.Raise(new EnrolledDomainEvent(id, userId, courseId));
        return enrollment;
    }

    public bool HasCompleted(Guid lessonId) =>
        _progress.Any(p => p.LessonId == lessonId && p.IsCompleted);

    /// <summary>
    /// Records that the learner opened a lesson, moving the resume point.
    ///
    /// <para>
    /// One UPDATE per lesson view for a signed-in learner, which is the highest-frequency write on
    /// the platform. Accepted knowingly: it is a single row, on a single index, and the alternative
    /// — deriving the resume point from the furthest completed lesson — answers a different question
    /// and answers it wrongly for the learner who is midway through something.
    /// </para>
    /// </summary>
    public void Visit(Guid lessonId, DateTimeOffset now)
    {
        LastLessonId = lessonId;
        LastAccessedAt = now;
    }

    /// <summary>
    /// Marks a lesson finished. <b>Idempotent</b>: completing an already-complete lesson keeps the
    /// original timestamp rather than moving it, so a double-tapped button cannot rewrite when the
    /// learner actually got there.
    /// </summary>
    public void CompleteLesson(Guid lessonId, DateTimeOffset now)
    {
        var existing = _progress.FirstOrDefault(p => p.LessonId == lessonId);

        if (existing is null)
        {
            _progress.Add(new LessonProgress(Guid.NewGuid(), Id, lessonId, now));
        }
        else
        {
            existing.Complete(now);
        }

        Visit(lessonId, now);
    }

    /// <summary>
    /// Un-marks a lesson, for the learner who ticked the wrong row. Leaves
    /// <see cref="CompletedAt"/> alone — see <see cref="TryComplete"/>.
    /// </summary>
    public Result ReopenLesson(Guid lessonId)
    {
        var existing = _progress.FirstOrDefault(p => p.LessonId == lessonId);
        if (existing is null || !existing.IsCompleted)
            return Result.Failure(Error.NotFound("That lesson is not marked complete."));

        existing.Reopen();
        return Result.Success();
    }

    /// <summary>
    /// Completes the course if every lesson in <paramref name="requiredLessonIds"/> is done.
    /// Returns true only on the transition, so a caller can raise the certificate/notification once.
    ///
    /// <para>
    /// <b>Completion is a moment, and it is never revoked</b> (LN-6). The obvious implementation is
    /// to derive it — "completed" means every lesson is ticked — but derived completion is
    /// retroactive: publish a new lesson into a course and everyone who ever finished it silently
    /// becomes unfinished, their certificates invalid, their dashboards wrong, for a lesson that did
    /// not exist when they were studying. Courses grow after launch by design (ADR-0013), so this
    /// would not be an edge case but the normal consequence of authoring.
    /// </para>
    /// <para>
    /// So the check runs against the lessons published <i>now</i>, and the answer is written down.
    /// Once stored it stands: a later lesson can leave a learner at 8/9 complete on a course they
    /// have genuinely completed, and that is the correct reading of both facts.
    /// </para>
    /// </summary>
    public bool TryComplete(IReadOnlyCollection<Guid> requiredLessonIds, DateTimeOffset now)
    {
        if (IsCompleted) return false;

        // An empty curriculum does not complete. Otherwise enrolling in a course whose lessons are
        // all unpublished would hand out a certificate for nothing.
        if (requiredLessonIds.Count == 0) return false;

        if (!requiredLessonIds.All(HasCompleted)) return false;

        CompletedAt = now;
        Raise(new CourseCompletedDomainEvent(Id, UserId, CourseId, now));
        return true;
    }
}

/// <summary>
/// One lesson's state within an enrollment.
///
/// <para>
/// Rows are <b>sparse</b> — written when a learner first touches a lesson, never pre-seeded from the
/// curriculum. Seeding would multiply every enrollment by its lesson count on day one to record,
/// almost entirely, that nothing has happened yet. Absence already says that.
/// </para>
/// </summary>
public sealed class LessonProgress : Entity
{
    public Guid EnrollmentId { get; private set; }

    /// <summary>
    /// The curriculum position, not the content body. A lesson that is re-pointed at a different
    /// body (<see cref="Lesson.UseContent"/>) is still the same lesson, and the learner has still
    /// done it.
    /// </summary>
    public Guid LessonId { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsCompleted => CompletedAt is not null;

    private LessonProgress() { } // EF

    internal LessonProgress(Guid id, Guid enrollmentId, Guid lessonId, DateTimeOffset completedAt)
        : base(id)
    {
        EnrollmentId = enrollmentId;
        LessonId = lessonId;
        CompletedAt = completedAt;
    }

    internal void Complete(DateTimeOffset now) => CompletedAt ??= now;

    internal void Reopen() => CompletedAt = null;
}
