using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Learning.Application;

/// <summary>
/// Use cases for the Course aggregate: curriculum authoring, and the read that joins it to the
/// bodies Content owns.
/// </summary>
public sealed class CourseService(
    ICourseRepository courses,
    ILessonContentReader bodies,
    IMediaDirectory media,
    IClock clock)
{
    // ---- Reads ----

    /// <summary>
    /// A published course as a learner sees it. Lessons whose bodies are not published are
    /// **omitted entirely** (ADR-0013) — a course can go live before every lesson is written, and
    /// the ones that are not yet published simply are not there.
    /// </summary>
    public async Task<CourseDto?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default)
    {
        var course = await courses.GetPublishedBySlugAsync(slug, ct);
        return course is null ? null : await ComposeAsync(course, publishedOnly: true, ct);
    }

    /// <summary>
    /// The authoring view: every lesson, including those whose bodies are still drafts. An author
    /// has to see the gaps — that is what makes the "publish before every lesson is done" decision
    /// an affordance rather than a trap.
    /// </summary>
    public async Task<CourseDto?> GetForAuthoringAsync(Guid id, CancellationToken ct = default)
    {
        var course = await courses.GetByIdAsync(id, ct);
        return course is null ? null : await ComposeAsync(course, publishedOnly: false, ct);
    }

    /// <summary>
    /// One published lesson of a published course, with its neighbours.
    ///
    /// <para>
    /// Composed from the same published-only view the course page uses, so a lesson that is hidden
    /// there cannot be reachable here — the two reads cannot disagree about what a learner may see,
    /// because there is only one rule and both go through it.
    /// </para>
    /// </summary>
    public async Task<LessonPageDto?> GetLessonPageAsync(
        string courseSlug, string lessonSlug, CancellationToken ct = default)
    {
        var course = await courses.GetPublishedBySlugAsync(courseSlug, ct);
        if (course is null) return null;

        var composed = await ComposeAsync(course, publishedOnly: true, ct);

        // Flattened across modules: a learner moves through one sequence, and a prev/next that
        // stopped at a section break would be the data model showing through the page.
        var ordered = composed.Modules
            .SelectMany(m => m.Lessons.Select(l => (Module: m, Lesson: l)))
            .ToList();

        var index = ordered.FindIndex(x => x.Lesson.Slug == lessonSlug);
        if (index < 0) return null;

        var (module, lesson) = ordered[index];

        static LessonLinkDto? Link(List<(CourseModuleDto Module, LessonDto Lesson)> all, int at) =>
            at >= 0 && at < all.Count
                ? new LessonLinkDto(all[at].Lesson.Id, all[at].Lesson.Slug, all[at].Lesson.Title)
                : null;

        return new LessonPageDto(
            course.Id,
            course.Slug.Value,
            course.Title,
            module.Title,
            index + 1,
            ordered.Count,
            lesson,
            Link(ordered, index - 1),
            Link(ordered, index + 1),
            // Only this lesson's body is resolved — the neighbours are links, and a link has no images.
            await ResolveMediaAsync(lesson.Blocks, ct));
    }

    /// <summary>
    /// Resolves the media ids an image block carries into renderable refs, in one batch call. Mirrors
    /// how Content resolves article images (ADR-0008): a lesson and an article are one primitive, so a
    /// lesson body must render its images the same way. Returns an empty map when the body carries
    /// none, so the common text-only lesson costs no media query.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, MediaRefDto>> ResolveMediaAsync(
        IReadOnlyList<ContentBlockView> blocks, CancellationToken ct)
    {
        var mediaIds = blocks
            .Where(b => string.Equals(b.Type, "image", StringComparison.OrdinalIgnoreCase))
            .Select(b => b.Data?["mediaId"]?.ToString())
            .Where(id => Guid.TryParse(id, out _))
            .Select(id => Guid.Parse(id!))
            .Distinct()
            .ToArray();

        if (mediaIds.Length == 0)
            return new Dictionary<string, MediaRefDto>();

        var resolved = await media.GetMediaAsync(mediaIds, ct);
        return resolved.ToDictionary(
            entry => entry.Key.ToString(),
            entry => new MediaRefDto(
                entry.Value.Url,
                entry.Value.AltText,
                entry.Value.Width,
                entry.Value.Height,
                entry.Value.Variants
                    .Select(v => new MediaVariantRefDto(v.Name, v.Url, v.Width, v.Height))
                    .ToList()));
    }

    public async Task<PagedResult<CourseSummaryDto>> ListPublishedAsync(
        PageRequest page, CancellationToken ct = default)
    {
        var result = await courses.ListPublishedAsync(page, ct);
        return Map(result);
    }

    public async Task<PagedResult<CourseSummaryDto>> ListAllAsync(PageRequest page, CancellationToken ct = default)
    {
        var result = await courses.ListAllAsync(page, ct);
        return Map(result);
    }

    private static PagedResult<CourseSummaryDto> Map(PagedResult<Course> page) =>
        new(page.Items.Select(c => c.ToSummaryDto()).ToList(), page.Page, page.PageSize, page.Total);

    /// <summary>
    /// Joins a curriculum to its bodies in <b>one</b> batch call, however many lessons it has. A
    /// per-lesson lookup would be an N+1 on a learner's hottest page, which is exactly why
    /// <see cref="ILessonContentReader"/> is batch-shaped.
    /// </summary>
    private async Task<CourseDto> ComposeAsync(Course course, bool publishedOnly, CancellationToken ct)
    {
        var contentIds = course.Modules
            .SelectMany(m => m.Lessons)
            .Select(l => l.ContentUnitId)
            .Distinct()
            .ToArray();

        var resolved = contentIds.Length == 0
            ? new Dictionary<Guid, LessonContentView>()
            : await bodies.GetLessonContentAsync(contentIds, ct);

        var modules = new List<CourseModuleDto>();

        foreach (var module in course.Modules)
        {
            var lessons = new List<LessonDto>();

            foreach (var lesson in module.Lessons)
            {
                resolved.TryGetValue(lesson.ContentUnitId, out var body);

                // A body that is missing entirely (deleted out from under the lesson) counts as
                // unpublished rather than throwing — a course must stay renderable.
                var isPublished = body?.PublishedAt is not null;

                if (publishedOnly && !isPublished) continue;

                lessons.Add(new LessonDto(
                    lesson.Id,
                    lesson.ContentUnitId,
                    body?.Slug ?? string.Empty,
                    body?.Title ?? "Untitled lesson",
                    body?.Summary ?? string.Empty,
                    lesson.Order,
                    lesson.EstimatedMinutes,
                    lesson.Difficulty.ToWire(),
                    lesson.Objectives,
                    lesson.PrerequisiteLessonIds,
                    isPublished,
                    body?.Blocks ?? []));
            }

            // An empty module is dropped from the learner's view but kept in the authoring one: a
            // section with nothing published in it is a heading with nothing under it.
            if (publishedOnly && lessons.Count == 0) continue;

            modules.Add(new CourseModuleDto(module.Id, module.Title, module.Summary, module.Order, lessons));
        }

        return new CourseDto(
            course.Id, course.Slug.Value, course.Title, course.Summary,
            course.Status.ToWire(), course.Difficulty.ToWire(),
            course.LessonCount, course.EstimatedMinutes, course.PublishedAt, modules);
    }

    // ---- Authoring ----

    public async Task<Result<CourseDto>> CreateAsync(CreateCourseRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result.Failure<CourseDto>(Error.Validation("Title is required."));

        Slug slug;
        try
        {
            slug = string.IsNullOrWhiteSpace(request.Slug)
                ? Slug.FromText(request.Title)
                : Slug.Create(request.Slug!);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<CourseDto>(Error.Validation(ex.Message));
        }

        if (await courses.SlugExistsAsync(slug.Value, ct: ct))
            return Result.Failure<CourseDto>(new Error("slug_taken", $"The slug '{slug.Value}' is already in use."));

        var course = Course.CreateDraft(
            Guid.NewGuid(), slug, request.Title, request.Summary,
            LearningMapping.ParseDifficulty(request.Difficulty));

        await courses.AddAsync(course, ct);
        await courses.SaveChangesAsync(ct);

        return Result.Success(await ComposeAsync(course, publishedOnly: false, ct));
    }

    public Task<Result<CourseDto>> UpdateAsync(Guid id, UpdateCourseRequest request, CancellationToken ct = default)
        => MutateAsync(id, course =>
        {
            course.Describe(request.Title, request.Summary, LearningMapping.ParseDifficulty(request.Difficulty));
            return Result.Success();
        }, ct);

    public Task<Result<CourseDto>> AddModuleAsync(Guid id, AddModuleRequest request, CancellationToken ct = default)
        => MutateAsync(id, course =>
        {
            course.AddModule(Guid.NewGuid(), request.Title);
            return Result.Success();
        }, ct);

    public Task<Result<CourseDto>> UpdateModuleAsync(
        Guid id, Guid moduleId, UpdateModuleRequest request, CancellationToken ct = default)
        => MutateAsync(id, course =>
        {
            var module = course.FindModule(moduleId);
            if (module is null) return Result.Failure(Error.NotFound("Module not found in this course."));

            module.Rename(request.Title, request.Summary);
            return Result.Success();
        }, ct);

    public Task<Result<CourseDto>> RemoveModuleAsync(Guid id, Guid moduleId, CancellationToken ct = default)
        => MutateAsync(id, course => course.RemoveModule(moduleId), ct);

    public Task<Result<CourseDto>> ReorderModulesAsync(Guid id, ReorderRequest request, CancellationToken ct = default)
        => MutateAsync(id, course => course.ReorderModules(request.OrderedIds), ct);

    public Task<Result<CourseDto>> AddLessonAsync(
        Guid id, Guid moduleId, AddLessonRequest request, CancellationToken ct = default)
        => MutateAsync(id, course =>
        {
            var module = course.FindModule(moduleId);
            if (module is null) return Result.Failure(Error.NotFound("Module not found in this course."));

            // The body's existence is not verified here. Content owns it, and a lesson pointing at a
            // body that is later removed already has to render gracefully — so a check now would buy
            // a moment's tidiness and no invariant.
            var added = module.AddLesson(Guid.NewGuid(), request.ContentUnitId);
            return added.IsFailure ? Result.Failure(added.Error) : Result.Success();
        }, ct);

    public Task<Result<CourseDto>> UpdateLessonAsync(
        Guid id, Guid moduleId, Guid lessonId, UpdateLessonRequest request, CancellationToken ct = default)
        => MutateAsync(id, course =>
        {
            var lesson = course.FindModule(moduleId)?.Lessons.FirstOrDefault(l => l.Id == lessonId);
            if (lesson is null) return Result.Failure(Error.NotFound("Lesson not found in this module."));

            lesson.Describe(
                request.EstimatedMinutes,
                LearningMapping.ParseDifficulty(request.Difficulty),
                request.Objectives);

            if (request.PrerequisiteLessonIds is not null)
                lesson.RequirePrerequisites(request.PrerequisiteLessonIds);

            return Result.Success();
        }, ct);

    public Task<Result<CourseDto>> RemoveLessonAsync(
        Guid id, Guid moduleId, Guid lessonId, CancellationToken ct = default)
        => MutateAsync(id, course =>
        {
            var module = course.FindModule(moduleId);
            return module is null
                ? Result.Failure(Error.NotFound("Module not found in this course."))
                : module.RemoveLesson(lessonId);
        }, ct);

    public Task<Result<CourseDto>> ReorderLessonsAsync(
        Guid id, Guid moduleId, ReorderRequest request, CancellationToken ct = default)
        => MutateAsync(id, course =>
        {
            var module = course.FindModule(moduleId);
            return module is null
                ? Result.Failure(Error.NotFound("Module not found in this course."))
                : module.ReorderLessons(request.OrderedIds);
        }, ct);

    public Task<Result<CourseDto>> PublishAsync(Guid id, CancellationToken ct = default)
        => MutateAsync(id, course => course.Publish(clock.UtcNow), ct);

    public Task<Result<CourseDto>> UnpublishAsync(Guid id, CancellationToken ct = default)
        => MutateAsync(id, course => course.Unpublish(), ct);

    /// <summary>
    /// Loads the course, applies one domain operation, saves, and returns the authoring view.
    ///
    /// Every mutation goes through here because the course is the aggregate root: a reorder that
    /// touches a dozen rows is one load and one save, and a failed rule leaves nothing written.
    /// </summary>
    private async Task<Result<CourseDto>> MutateAsync(
        Guid id, Func<Course, Result> mutate, CancellationToken ct)
    {
        var course = await courses.GetByIdAsync(id, ct);
        if (course is null)
            return Result.Failure<CourseDto>(Error.NotFound("Course not found."));

        var result = mutate(course);
        if (result.IsFailure)
            return Result.Failure<CourseDto>(result.Error);

        await courses.SaveChangesAsync(ct);
        return Result.Success(await ComposeAsync(course, publishedOnly: false, ct));
    }
}
