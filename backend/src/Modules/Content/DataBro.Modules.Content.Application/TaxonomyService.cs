using DataBro.Modules.Content.Domain;
using DataBro.Platform.Results;

namespace DataBro.Modules.Content.Application;

/// <summary>
/// Use cases for the Content module's taxonomy: categories (hierarchical, one per article) and tags
/// (flat, many per article). Rules TX-1 … TX-3 and CT-11 (docs/BUSINESS_RULES.md).
/// </summary>
public sealed class TaxonomyService(ICategoryRepository categories, ITagRepository tags)
{
    // ---- Categories ----

    public async Task<Result<CategoryDto>> CreateCategoryAsync(
        CreateCategoryRequest request, CancellationToken ct = default)
    {
        var slugResult = ResolveSlug(request.Slug, request.Name);
        if (slugResult.IsFailure)
            return Result.Failure<CategoryDto>(slugResult.Error);

        var slug = slugResult.Value;

        // TX-1: unique among categories. A tag may hold the same slug — different URL namespace.
        if (await categories.SlugExistsAsync(slug.Value, ct))
            return Result.Failure<CategoryDto>(
                new Error("slug_taken", $"The category slug '{slug.Value}' is already in use."));

        if (request.ParentId is { } parentId && await categories.GetByIdAsync(parentId, ct) is null)
            return Result.Failure<CategoryDto>(Error.Validation("The parent category does not exist."));

        var category = Category.Create(
            Guid.NewGuid(), slug, request.Name, request.ParentId, request.Description, request.Order);

        await categories.AddAsync(category, ct);
        await categories.SaveChangesAsync(ct);

        return Result.Success(category.ToDto());
    }

    public async Task<Result<CategoryDto>> UpdateCategoryAsync(
        Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await categories.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure<CategoryDto>(Error.NotFound("Category not found."));

        category.Update(request.Name, request.Description, request.Order);

        if (request.ParentId != category.ParentId)
        {
            if (request.ParentId is { } parentId)
            {
                if (await categories.GetByIdAsync(parentId, ct) is null)
                    return Result.Failure<CategoryDto>(Error.Validation("The parent category does not exist."));
            }

            // TX-3: the domain rejects cycles, but it cannot query — hand it the prospective
            // parent's ancestor chain to decide against.
            var ancestry = request.ParentId is { } target
                ? await categories.GetAncestryAsync(target, ct)
                : [];

            // The target itself counts as part of the chain being moved beneath.
            var chain = request.ParentId is { } p ? new List<Guid>(ancestry) { p } : [];

            var move = category.MoveTo(request.ParentId, chain);
            if (move.IsFailure)
                return Result.Failure<CategoryDto>(move.Error);
        }

        await categories.SaveChangesAsync(ct);
        return Result.Success(category.ToDto());
    }

    /// <summary>
    /// TX-2: a category still classifying articles cannot be removed. Refuses with a conflict that
    /// names the count, so an editor knows how much reassignment is pending rather than getting a
    /// bare failure.
    /// </summary>
    public async Task<Result> DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var category = await categories.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure(Error.NotFound("Category not found."));

        var articleCount = await categories.CountArticlesAsync(id, ct);
        if (articleCount > 0)
            return Result.Failure(Error.Conflict(
                $"This category still classifies {articleCount} article(s). Reassign them before deleting it."));

        var children = await categories.ListAllAsync(ct);
        if (children.Any(c => c.ParentId == id))
            return Result.Failure(Error.Conflict(
                "This category still has child categories. Move or delete them before deleting it."));

        categories.Remove(category);
        await categories.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken ct = default)
    {
        var all = await categories.ListAllAsync(ct);

        // One grouped query for every count, rather than one query per category.
        var counts = await categories.CountPublishedArticlesAsync(ct);

        return all
            .Select(c => c.ToDto() with { ArticleCount = counts.GetValueOrDefault(c.Id) })
            .ToList();
    }

    /// <summary>The category plus its ancestor trail (root first) for breadcrumbs.</summary>
    public async Task<CategoryWithAncestorsDto?> GetCategoryBySlugAsync(
        string slug, CancellationToken ct = default)
    {
        var category = await categories.GetBySlugAsync(slug, ct);
        if (category is null) return null;

        var ancestryIds = await categories.GetAncestryAsync(category.Id, ct);
        if (ancestryIds.Count == 0)
            return new CategoryWithAncestorsDto(category.ToDto(), []);

        var all = (await categories.ListAllAsync(ct)).ToDictionary(c => c.Id);

        // GetAncestryAsync returns nearest-first; breadcrumbs read root-first.
        var ancestors = ancestryIds
            .Select(id => all.GetValueOrDefault(id))
            .Where(c => c is not null)
            .Select(c => c!.ToDto())
            .Reverse()
            .ToList();

        return new CategoryWithAncestorsDto(category.ToDto(), ancestors);
    }

    // ---- Tags ----

    public async Task<Result<TaxonomyTermDto>> CreateTagAsync(
        CreateTagRequest request, CancellationToken ct = default)
    {
        var slugResult = ResolveSlug(request.Slug, request.Name);
        if (slugResult.IsFailure)
            return Result.Failure<TaxonomyTermDto>(slugResult.Error);

        var slug = slugResult.Value;

        if (await tags.SlugExistsAsync(slug.Value, ct))
            return Result.Failure<TaxonomyTermDto>(
                new Error("slug_taken", $"The tag slug '{slug.Value}' is already in use."));

        var tag = Tag.Create(Guid.NewGuid(), slug, request.Name);
        await tags.AddAsync(tag, ct);
        await tags.SaveChangesAsync(ct);

        return Result.Success(tag.ToTermDto());
    }

    public async Task<Result<TaxonomyTermDto>> UpdateTagAsync(
        Guid id, UpdateTagRequest request, CancellationToken ct = default)
    {
        var tag = await tags.GetByIdAsync(id, ct);
        if (tag is null)
            return Result.Failure<TaxonomyTermDto>(Error.NotFound("Tag not found."));

        tag.Rename(request.Name);
        await tags.SaveChangesAsync(ct);
        return Result.Success(tag.ToTermDto());
    }

    /// <summary>
    /// Soft-deletes a tag. Unlike a category (TX-2) this is always allowed: a tag is a label, not a
    /// classification an article depends on, and the soft-delete filter removes it from every
    /// article's tag list automatically.
    /// </summary>
    public async Task<Result> DeleteTagAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await tags.GetByIdAsync(id, ct);
        if (tag is null)
            return Result.Failure(Error.NotFound("Tag not found."));

        tags.Remove(tag);
        await tags.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<IReadOnlyList<TaxonomyTermDto>> ListTagsAsync(CancellationToken ct = default)
        => (await tags.ListAllAsync(ct)).Select(t => t.ToTermDto()).ToList();

    public async Task<TaxonomyTermDto?> GetTagBySlugAsync(string slug, CancellationToken ct = default)
        => (await tags.GetBySlugAsync(slug, ct))?.ToTermDto();

    // ---- Shared ----

    private static Result<Slug> ResolveSlug(string? explicitSlug, string fallbackText)
    {
        try
        {
            return Result.Success(string.IsNullOrWhiteSpace(explicitSlug)
                ? Slug.FromText(fallbackText)
                : Slug.Create(explicitSlug));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Slug>(Error.Validation(ex.Message));
        }
    }
}
