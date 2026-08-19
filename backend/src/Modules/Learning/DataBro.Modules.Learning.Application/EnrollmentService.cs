using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Results;

namespace DataBro.Modules.Learning.Application;

/// <summary>
/// Use cases for a learner's own progress: joining a course, moving through it, finishing it.
///
/// <para>
/// Every method takes the learner's id explicitly rather than reading it from an ambient
/// <c>ICurrentUser</c>. On a surface where the id <i>is</i> the authorization boundary — this is the
/// data one learner is allowed to see and change — an implicit parameter is the kind that gets
/// forgotten in one branch and reads someone else's progress. Passing it makes every call site
/// state whose progress it means.
/// </para>
/// </summary>
public sealed class EnrollmentService(
    IEnrollmentRepository enrollments,
    ICourseRepository courses,
    ILessonContentReader bodies,
    IQuizGate quizGate,
    IClock clock)
{
    /// <summary>
    /// Enrols the learner, or returns the enrollment they already have.
    ///
    /// <para>
    /// <b>Idempotent by design, not by accident.</b> A second enrol is a double-tapped button or a
    /// retried request, and answering it with a 409 would make the client handle an error that is
    /// not one. The unique index still exists for the concurrent case, and losing that race is
    /// handled the same way: re-read and return the winner.
    /// </para>
    /// </summary>
    public async Task<Result<EnrollmentDto>> EnrolAsync(
        Guid userId, string courseSlug, CancellationToken ct = default)
    {
        var course = await courses.GetPublishedBySlugAsync(courseSlug, ct);
        if (course is null)
            return Result.Failure<EnrollmentDto>(Error.NotFound("Course not found."));

        var existing = await enrollments.GetAsync(userId, course.Id, ct);
        if (existing is not null)
            return Result.Success(await ComposeAsync(existing, course, ct));

        var enrollment = Enrollment.Start(Guid.NewGuid(), userId, course.Id, clock.UtcNow);
        await enrollments.AddAsync(enrollment, ct);

        if (!await enrollments.SaveHandlingDuplicateAsync(ct))
        {
            // The other request won the race. Its row is the real one.
            var winner = await enrollments.GetAsync(userId, course.Id, ct);
            if (winner is null)
                return Result.Failure<EnrollmentDto>(Error.Conflict("Enrolment failed; please retry."));

            return Result.Success(await ComposeAsync(winner, course, ct));
        }

        return Result.Success(await ComposeAsync(enrollment, course, ct));
    }

    /// <summary>The learner's progress in one course, or null when they are not enrolled.</summary>
    public async Task<EnrollmentDto?> GetAsync(Guid userId, string courseSlug, CancellationToken ct = default)
    {
        var course = await courses.GetPublishedBySlugAsync(courseSlug, ct);
        if (course is null) return null;

        var enrollment = await enrollments.GetAsync(userId, course.Id, ct);
        return enrollment is null ? null : await ComposeAsync(enrollment, course, ct);
    }

    /// <summary>The learner's dashboard: everything they are enrolled in, most recent first.</summary>
    public async Task<PagedResult<EnrollmentDto>> ListForUserAsync(
        Guid userId, PageRequest page, CancellationToken ct = default)
    {
        var result = await enrollments.ListForUserAsync(userId, page, ct);
        if (result.Items.Count == 0)
            return new PagedResult<EnrollmentDto>([], result.Page, result.PageSize, result.Total);

        // One batch load for every course on the page, rather than one per card.
        var courseIds = result.Items.Select(e => e.CourseId).Distinct().ToArray();
        var loaded = (await courses.GetByIdsAsync(courseIds, ct)).ToDictionary(c => c.Id);

        var items = new List<EnrollmentDto>();
        foreach (var enrollment in result.Items)
        {
            // A course removed out from under an enrollment leaves the card renderable rather than
            // throwing, the same tolerance a curriculum shows for a missing body.
            loaded.TryGetValue(enrollment.CourseId, out var course);
            items.Add(await ComposeAsync(enrollment, course, ct));
        }

        return new PagedResult<EnrollmentDto>(items, result.Page, result.PageSize, result.Total);
    }

    /// <summary>
    /// Moves the resume point. Cheap and frequent — the learner opened a lesson, nothing more.
    /// </summary>
    public Task<Result<EnrollmentDto>> VisitLessonAsync(
        Guid userId, string courseSlug, Guid lessonId, CancellationToken ct = default)
        => MutateAsync(userId, courseSlug, lessonId, (enrollment, _) =>
        {
            enrollment.Visit(lessonId, clock.UtcNow);
            return Task.FromResult(Result.Success());
        }, ct);

    /// <summary>
    /// Marks a lesson complete, then checks whether that finished the course.
    ///
    /// <para>
    /// A lesson with a published quiz cannot be completed until the learner has passed it (AS-9,
    /// decided in D-1). The gate is asked here, at completion time, rather than driven by the submit
    /// event — a learner who passes and immediately clicks complete must not be refused because an
    /// outbox has not caught up (see <see cref="IQuizGate"/>). A lesson with no quiz is unaffected,
    /// and a quiz added <i>after</i> a lesson was completed does not revoke that completion — the gate
    /// only stands in front of a completion still to be made, the same one-way stance LN-6 takes.
    /// </para>
    /// </summary>
    public Task<Result<EnrollmentDto>> CompleteLessonAsync(
        Guid userId, string courseSlug, Guid lessonId, CancellationToken ct = default)
        => MutateAsync(userId, courseSlug, lessonId, async (enrollment, publishedLessonIds) =>
        {
            // The gate stands in front of a completion still to be made — never behind one already
            // made. Re-completing a lesson the learner has finished must stay the no-op it is
            // (CompleteLesson is idempotent), even if a quiz was added afterwards: a completion is a
            // moment that stands (LN-6), and a later quiz does not reach back and revoke it.
            if (!enrollment.HasCompleted(lessonId)
                && await quizGate.IsCompletionBlockedAsync(userId, lessonId, ct))
                return Result.Failure(Error.Rule("Pass this lesson's quiz before marking it complete."));

            enrollment.CompleteLesson(lessonId, clock.UtcNow);
            enrollment.TryComplete(publishedLessonIds, clock.UtcNow);
            return Result.Success();
        }, ct);

    /// <summary>
    /// Un-marks a lesson. Deliberately does <b>not</b> re-open a completed course — see
    /// <see cref="Enrollment.TryComplete"/>.
    /// </summary>
    public Task<Result<EnrollmentDto>> ReopenLessonAsync(
        Guid userId, string courseSlug, Guid lessonId, CancellationToken ct = default)
        => MutateAsync(userId, courseSlug, lessonId, (enrollment, _) =>
            Task.FromResult(enrollment.ReopenLesson(lessonId)), ct);

    /// <summary>
    /// The shared shape of every progress write: resolve the course, check the learner is enrolled,
    /// check the lesson is one they are allowed to record against, mutate, save.
    /// </summary>
    private async Task<Result<EnrollmentDto>> MutateAsync(
        Guid userId,
        string courseSlug,
        Guid lessonId,
        Func<Enrollment, IReadOnlyCollection<Guid>, Task<Result>> mutate,
        CancellationToken ct)
    {
        var course = await courses.GetPublishedBySlugAsync(courseSlug, ct);
        if (course is null)
            return Result.Failure<EnrollmentDto>(Error.NotFound("Course not found."));

        var enrollment = await enrollments.GetAsync(userId, course.Id, ct);
        if (enrollment is null)
            return Result.Failure<EnrollmentDto>(Error.Rule("You are not enrolled in this course."));

        var publishedLessonIds = (await PublishedLessonsAsync(course, ct)).Keys.ToHashSet();

        // Progress may only be recorded against a lesson the learner can actually reach. Without
        // this, a client could tick a lesson whose body is still a draft — or one from an entirely
        // different course — and complete a course it had never opened.
        if (!publishedLessonIds.Contains(lessonId))
            return Result.Failure<EnrollmentDto>(Error.NotFound("Lesson not found in this course."));

        var result = await mutate(enrollment, publishedLessonIds);
        if (result.IsFailure) return Result.Failure<EnrollmentDto>(result.Error);

        await enrollments.SaveChangesAsync(ct);
        return Result.Success(await ComposeAsync(enrollment, course, ct));
    }

    /// <summary>
    /// The lessons that count: in this course, with a body Content has published.
    ///
    /// <para>
    /// Resolved live rather than stored on the course, because publication state belongs to Content
    /// and a cached copy here would be the thing that goes stale (rule 10). It is the same batch
    /// call the curriculum read already makes.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, string>> PublishedLessonsAsync(
        Course course, CancellationToken ct)
    {
        var lessons = course.Modules.SelectMany(m => m.Lessons).ToArray();
        if (lessons.Length == 0) return new Dictionary<Guid, string>();

        var resolved = await bodies.GetLessonContentAsync(
            lessons.Select(l => l.ContentUnitId).Distinct().ToArray(), ct);

        // Keyed by lesson id, valued by the body's slug. Both are needed and both come from the
        // same resolve, so returning a map rather than a set costs nothing and saves the caller a
        // second pass to turn a resume point into a URL.
        var map = new Dictionary<Guid, string>();

        foreach (var lesson in lessons)
        {
            if (resolved.TryGetValue(lesson.ContentUnitId, out var body) && body.PublishedAt is not null)
                map[lesson.Id] = body.Slug;
        }

        return map;
    }

    private async Task<EnrollmentDto> ComposeAsync(Enrollment enrollment, Course? course, CancellationToken ct)
    {
        var published = course is null
            ? new Dictionary<Guid, string>()
            : await PublishedLessonsAsync(course, ct);

        var total = published.Count;

        var completed = enrollment.Progress
            .Where(p => p.IsCompleted)
            .Select(p => p.LessonId)
            .ToList();

        // Percent is derived at read time, never stored: it is a function of two numbers that both
        // move, and a stored copy would be wrong the moment a lesson was published.
        //
        // Capped at 100 for the learner who completed a course before it grew. Their CompletedAt
        // stands (LN-6), but their ratio can legitimately exceed the denominator, and "104%
        // complete" on a dashboard reads as a bug rather than as the honest consequence it is.
        var percent = total == 0 ? 0 : Math.Min(100, (int)Math.Round(completed.Count * 100.0 / total));

        // The resume point as a URL, not just an id. Null when the lesson has since been unpublished
        // or removed from the curriculum: a Resume button is only worth offering if it leads
        // somewhere, and the id alone cannot tell a client that.
        var lastLessonSlug = enrollment.LastLessonId is { } last && published.TryGetValue(last, out var slug)
            ? slug
            : null;

        return new EnrollmentDto(
            enrollment.Id,
            enrollment.CourseId,
            course?.Slug.Value ?? string.Empty,
            course?.Title ?? "Unavailable course",
            enrollment.EnrolledAt,
            enrollment.CompletedAt,
            enrollment.LastLessonId,
            lastLessonSlug,
            enrollment.LastAccessedAt,
            total,
            completed.Count,
            percent,
            completed);
    }
}
