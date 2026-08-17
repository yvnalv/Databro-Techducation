using DataBro.Modules.Content.Domain;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Application;

/// <summary>
/// Authoring use cases for lesson bodies (ADR-0012).
///
/// <para>
/// Deliberately much smaller than <see cref="ArticleService"/>, and the difference is the point: no
/// taxonomy, no SEO metadata, no author resolution, no redirects. A lesson body is the content
/// engine and nothing else — everything else about a lesson belongs to Learning.
/// </para>
/// </summary>
public sealed class LessonContentService(
    ILessonContentRepository repository,
    IContentSlugRegistry slugs,
    IClock clock)
{
    public async Task<Result<LessonContentDto>> CreateAsync(
        CreateLessonContentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result.Failure<LessonContentDto>(Error.Validation("Title is required."));

        Slug slug;
        try
        {
            slug = string.IsNullOrWhiteSpace(request.Slug)
                ? Slug.FromText(request.Title)
                : Slug.Create(request.Slug!);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<LessonContentDto>(Error.Validation(ex.Message));
        }

        // Checked across articles *and* lesson bodies: both are URLs on one origin (ADR-0012).
        if (await slugs.IsTakenAsync(slug.Value, ct: ct))
            return Result.Failure<LessonContentDto>(
                new Error("slug_taken", $"The slug '{slug.Value}' is already in use."));

        var lesson = LessonContent.CreateDraft(
            Guid.NewGuid(), slug, request.Title, request.Summary, request.Content.ToDomain());

        await repository.AddAsync(lesson, ct);
        await repository.SaveChangesAsync(ct);

        return Result.Success(lesson.ToDto());
    }

    public Task<Result<LessonContentDto>> UpdateAsync(
        Guid id, UpdateLessonContentRequest request, CancellationToken ct = default)
        => MutateAsync(id, lesson =>
        {
            lesson.UpdateDraft(request.Title, request.Summary, request.Content.ToDomain());
            return Result.Success();
        }, ct);

    public Task<Result<LessonContentDto>> PublishAsync(Guid id, CancellationToken ct = default)
        => MutateAsync(id, lesson => lesson.Publish(clock.UtcNow), ct);

    /// <summary>
    /// Takes a body down. Deliberately does <b>not</b> check whether a published course uses it —
    /// Content cannot ask Learning that without depending on it (ADR-0013). The lesson simply
    /// disappears from the course, and the CMS is what warns an author beforehand.
    /// </summary>
    public Task<Result<LessonContentDto>> UnpublishAsync(Guid id, CancellationToken ct = default)
        => MutateAsync(id, lesson => lesson.Unpublish(), ct);

    public Task<Result<LessonContentDto>> RestoreVersionAsync(
        Guid id, int version, CancellationToken ct = default)
        => MutateAsync(id, lesson => lesson.RestoreVersion(version), ct);

    public async Task<LessonContentDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var lesson = await repository.GetByIdAsync(id, ct);
        return lesson?.ToDto();
    }

    public async Task<IReadOnlyList<ArticleVersionSummaryDto>?> ListVersionsAsync(
        Guid id, CancellationToken ct = default)
    {
        var lesson = await repository.GetByIdAsync(id, ct);
        if (lesson is null) return null;

        return lesson.Versions
            .OrderByDescending(v => v.Version)
            .Select(v => new ArticleVersionSummaryDto(
                v.Version, v.Title, v.Summary, v.Blocks.EstimateReadingTimeMinutes(),
                v.CreatedAt, v.Version == lesson.CurrentVersion))
            .ToList();
    }

    public async Task<PagedResult<LessonContentSummaryDto>> ListAsync(
        PageRequest page, CancellationToken ct = default)
    {
        var result = await repository.ListAllAsync(page, ct);

        return new PagedResult<LessonContentSummaryDto>(
            result.Items.Select(l => l.ToSummaryDto()).ToList(),
            result.Page, result.PageSize, result.Total);
    }

    private async Task<Result<LessonContentDto>> MutateAsync(
        Guid id, Func<LessonContent, Result> mutate, CancellationToken ct)
    {
        var lesson = await repository.GetByIdAsync(id, ct);
        if (lesson is null)
            return Result.Failure<LessonContentDto>(Error.NotFound("Lesson content not found."));

        var result = mutate(lesson);
        if (result.IsFailure)
            return Result.Failure<LessonContentDto>(result.Error);

        await repository.SaveChangesAsync(ct);
        return Result.Success(lesson.ToDto());
    }
}
