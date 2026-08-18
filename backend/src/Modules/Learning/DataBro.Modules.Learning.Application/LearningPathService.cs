using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Learning.Application;

/// <summary>
/// Use cases for the LearningPath aggregate: curating a sequence of courses, and the read that
/// resolves those ids into cards.
/// </summary>
public sealed class LearningPathService(
    ILearningPathRepository paths,
    ICourseRepository courses,
    IClock clock)
{
    // ---- Reads ----

    public async Task<LearningPathDto?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default)
    {
        var path = await paths.GetPublishedBySlugAsync(slug, ct);
        return path is null ? null : await ComposeAsync(path, publishedOnly: true, ct);
    }

    public async Task<LearningPathDto?> GetForAuthoringAsync(Guid id, CancellationToken ct = default)
    {
        var path = await paths.GetByIdAsync(id, ct);
        return path is null ? null : await ComposeAsync(path, publishedOnly: false, ct);
    }

    public async Task<PagedResult<LearningPathDto>> ListPublishedAsync(
        PageRequest page, CancellationToken ct = default)
    {
        var result = await paths.ListPublishedAsync(page, ct);

        var composed = new List<LearningPathDto>();
        foreach (var path in result.Items)
            composed.Add(await ComposeAsync(path, publishedOnly: true, ct));

        return new PagedResult<LearningPathDto>(composed, result.Page, result.PageSize, result.Total);
    }

    /// <summary>Every path, drafts included — what the curator's listing needs.</summary>
    public async Task<PagedResult<LearningPathDto>> ListAllAsync(
        PageRequest page, CancellationToken ct = default)
    {
        var result = await paths.ListAllAsync(page, ct);

        var composed = new List<LearningPathDto>();
        foreach (var path in result.Items)
            composed.Add(await ComposeAsync(path, publishedOnly: false, ct));

        return new PagedResult<LearningPathDto>(composed, result.Page, result.PageSize, result.Total);
    }

    /// <summary>
    /// Resolves a path's course ids into cards, in the curated order.
    ///
    /// <para>
    /// One batch call however long the path, and the order comes from the <b>path</b> rather than
    /// from whatever the repository returned: the sequence is the entire point of a path, and a
    /// database's natural ordering is not it.
    /// </para>
    /// <para>
    /// On the public read, unpublished courses are <b>dropped</b> — the same rule a course applies to
    /// an unpublished lesson (LN-1/LN-2). A path can be curated ahead of the courses in it, and the
    /// ones that are not ready simply are not shown. The authoring view keeps them, so a curator
    /// sees the gap rather than wondering where a card went.
    /// </para>
    /// </summary>
    private async Task<LearningPathDto> ComposeAsync(
        LearningPath path, bool publishedOnly, CancellationToken ct)
    {
        var ids = path.CourseIds;

        var loaded = ids.Count == 0
            ? []
            : await courses.GetByIdsAsync(ids, ct);

        var byId = loaded.ToDictionary(c => c.Id);

        var cards = ids
            .Select(id => byId.TryGetValue(id, out var course) ? course : null)
            .Where(course => course is not null)
            .Select(course => course!)
            .Where(course => !publishedOnly || course.Status == CourseStatus.Published)
            .Select(course => course.ToSummaryDto())
            .ToList();

        return new LearningPathDto(
            path.Id,
            path.Slug.Value,
            path.Title,
            path.Summary,
            path.Status.ToWire(),
            path.Difficulty.ToWire(),
            path.PublishedAt,
            cards);
    }

    // ---- Authoring ----

    public async Task<Result<LearningPathDto>> CreateAsync(
        CreateLearningPathRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result.Failure<LearningPathDto>(Error.Validation("Title is required."));

        Slug slug;
        try
        {
            slug = string.IsNullOrWhiteSpace(request.Slug)
                ? Slug.FromText(request.Title)
                : Slug.Create(request.Slug!);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<LearningPathDto>(Error.Validation(ex.Message));
        }

        if (await paths.SlugExistsAsync(slug.Value, ct: ct))
            return Result.Failure<LearningPathDto>(
                new Error("slug_taken", $"The slug '{slug.Value}' is already in use."));

        var path = LearningPath.CreateDraft(
            Guid.NewGuid(), slug, request.Title, request.Summary,
            LearningMapping.ParseDifficulty(request.Difficulty));

        await paths.AddAsync(path, ct);
        await paths.SaveChangesAsync(ct);

        return Result.Success(await ComposeAsync(path, publishedOnly: false, ct));
    }

    public Task<Result<LearningPathDto>> UpdateAsync(
        Guid id, UpdateLearningPathRequest request, CancellationToken ct = default)
        => MutateAsync(id, path =>
        {
            path.Describe(request.Title, request.Summary, LearningMapping.ParseDifficulty(request.Difficulty));
            return Result.Success();
        }, ct);

    public Task<Result<LearningPathDto>> AddCourseAsync(
        Guid id, Guid courseId, CancellationToken ct = default)
        => MutateAsync(id, path =>
        {
            path.AddCourse(courseId);
            return Result.Success();
        }, ct);

    public Task<Result<LearningPathDto>> RemoveCourseAsync(
        Guid id, Guid courseId, CancellationToken ct = default)
        => MutateAsync(id, path =>
        {
            path.RemoveCourse(courseId);
            return Result.Success();
        }, ct);

    public Task<Result<LearningPathDto>> ReorderCoursesAsync(
        Guid id, ReorderRequest request, CancellationToken ct = default)
        => MutateAsync(id, path => path.ReorderCourses(request.OrderedIds), ct);

    public Task<Result<LearningPathDto>> PublishAsync(Guid id, CancellationToken ct = default)
        => MutateAsync(id, path => path.Publish(clock.UtcNow), ct);

    public Task<Result<LearningPathDto>> UnpublishAsync(Guid id, CancellationToken ct = default)
        => MutateAsync(id, path => path.Unpublish(), ct);

    private async Task<Result<LearningPathDto>> MutateAsync(
        Guid id, Func<LearningPath, Result> mutate, CancellationToken ct)
    {
        var path = await paths.GetByIdAsync(id, ct);
        if (path is null)
            return Result.Failure<LearningPathDto>(Error.NotFound("Learning path not found."));

        var result = mutate(path);
        if (result.IsFailure) return Result.Failure<LearningPathDto>(result.Error);

        await paths.SaveChangesAsync(ct);

        // The whole path comes back from every mutation, so a builder UI never reconciles a patch
        // against local state — the same contract the course builder relies on.
        return Result.Success(await ComposeAsync(path, publishedOnly: false, ct));
    }
}
