using DataBro.Modules.Content.Domain;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Results;

namespace DataBro.Modules.Content.Application;

/// <summary>Use cases for the Content module's Article aggregate (thin controllers call this).</summary>
public sealed class ArticleService(
    IArticleRepository repository,
    ICategoryRepository categories,
    ITagRepository tags,
    IClock clock,
    ICurrentUser currentUser,
    IUserDirectory userDirectory)
{
    // Fallback author if a request is somehow unauthenticated (authoring endpoints require auth).
    private static readonly Guid SystemAuthorId = new("00000000-0000-0000-0000-0000000000a1");

    public async Task<Result<ArticleDto>> CreateDraftAsync(CreateArticleRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result.Failure<ArticleDto>(Error.Validation("Title is required."));

        Slug slug;
        try
        {
            slug = string.IsNullOrWhiteSpace(request.Slug)
                ? Slug.FromText(request.Title)
                : Slug.Create(request.Slug!);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<ArticleDto>(Error.Validation(ex.Message));
        }

        if (await repository.SlugExistsAsync(slug.Value, ct))
            return Result.Failure<ArticleDto>(new Error("slug_taken", $"The slug '{slug.Value}' is already in use."));

        var visibility = Enum.TryParse<Visibility>(request.Visibility, ignoreCase: true, out var v)
            ? v
            : Visibility.Public;

        var article = Article.CreateDraft(
            Guid.NewGuid(),
            slug,
            request.Title,
            request.Summary,
            currentUser.UserId ?? request.AuthorId ?? SystemAuthorId,
            request.Content.ToDomain(),
            request.Locale,
            visibility,
            request.Seo?.ToDomain());

        var taxonomy = await ApplyTaxonomyAsync(article, request.CategoryId, request.TagIds, ct);
        if (taxonomy.IsFailure)
            return Result.Failure<ArticleDto>(taxonomy.Error);

        await repository.AddAsync(article, ct);
        await repository.SaveChangesAsync(ct);

        return Result.Success(article.ToDraftDto(await ResolveAsync([article], ct)));
    }

    public async Task<Result<ArticleDto>> UpdateDraftAsync(Guid id, UpdateArticleRequest request, CancellationToken ct = default)
    {
        var article = await repository.GetByIdAsync(id, ct);
        if (article is null)
            return Result.Failure<ArticleDto>(Error.NotFound("Article not found."));

        article.UpdateDraft(request.Title, request.Summary, request.Content.ToDomain(), request.Seo?.ToDomain());

        var taxonomy = await ApplyTaxonomyAsync(article, request.CategoryId, request.TagIds, ct);
        if (taxonomy.IsFailure)
            return Result.Failure<ArticleDto>(taxonomy.Error);

        await repository.SaveChangesAsync(ct);
        return Result.Success(article.ToDraftDto(await ResolveAsync([article], ct)));
    }

    public async Task<Result<ArticleDto>> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var article = await repository.GetByIdAsync(id, ct);
        if (article is null)
            return Result.Failure<ArticleDto>(Error.NotFound("Article not found."));

        var result = article.Publish(clock.UtcNow);
        if (result.IsFailure)
            return Result.Failure<ArticleDto>(result.Error);

        await repository.SaveChangesAsync(ct);
        return Result.Success(article.ToDto(await ResolveAsync([article], ct)));
    }

    public async Task<Result<ArticleDto>> UnpublishAsync(Guid id, CancellationToken ct = default)
    {
        var article = await repository.GetByIdAsync(id, ct);
        if (article is null)
            return Result.Failure<ArticleDto>(Error.NotFound("Article not found."));

        var result = article.Unpublish();
        if (result.IsFailure)
            return Result.Failure<ArticleDto>(result.Error);

        await repository.SaveChangesAsync(ct);
        return Result.Success(article.ToDraftDto(await ResolveAsync([article], ct)));
    }

    public async Task<ArticleDto?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default)
    {
        var article = await repository.GetPublishedBySlugAsync(slug, ct);
        return article is null ? null : article.ToDto(await ResolveAsync([article], ct));
    }

    public async Task<ArticleDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var article = await repository.GetByIdAsync(id, ct);
        return article is null ? null : article.ToDraftDto(await ResolveAsync([article], ct));
    }

    public async Task<PagedResult<ArticleSummaryDto>> ListPublishedAsync(
        PageRequest page,
        Guid? categoryId = null,
        Guid? tagId = null,
        CancellationToken ct = default)
    {
        var result = await repository.ListPublishedAsync(page, categoryId, tagId, ct);
        return await ToSummaryPageAsync(result, ct);
    }

    public async Task<PagedResult<ArticleSummaryDto>> ListAllAsync(PageRequest page, CancellationToken ct = default)
    {
        var result = await repository.ListAllAsync(page, ct);
        return await ToSummaryPageAsync(result, ct);
    }

    // ---- Taxonomy assignment ----

    /// <summary>
    /// Applies category and tag assignment, verifying both exist. Null means "leave unchanged" so a
    /// PATCH that omits taxonomy does not silently clear it; an empty tag list clears the tags.
    /// </summary>
    private async Task<Result> ApplyTaxonomyAsync(
        Article article, Guid? categoryId, IReadOnlyList<Guid>? tagIds, CancellationToken ct)
    {
        if (categoryId is { } id)
        {
            if (await categories.GetByIdAsync(id, ct) is null)
                return Result.Failure(Error.Validation("The specified category does not exist."));

            article.SetCategory(id);
        }

        if (tagIds is not null)
        {
            var resolved = await tags.GetByIdsAsync(tagIds, ct);

            // Reject unknown ids outright rather than silently dropping them: a caller that sent a
            // bad id should learn about it, not discover missing tags later.
            if (resolved.Count != tagIds.Distinct().Count())
                return Result.Failure(Error.Validation("One or more of the specified tags do not exist."));

            article.SetTags(tagIds);
        }

        return Result.Success();
    }

    // ---- Reference resolution ----

    private async Task<PagedResult<ArticleSummaryDto>> ToSummaryPageAsync(
        PagedResult<Article> page, CancellationToken ct)
    {
        var refs = await ResolveAsync(page.Items, ct);
        var items = page.Items.Select(a => a.ToSummaryDto(refs)).ToList();

        return new PagedResult<ArticleSummaryDto>(items, page.Page, page.PageSize, page.Total);
    }

    /// <summary>
    /// Resolves every out-of-aggregate reference for a set of articles in a fixed number of queries,
    /// regardless of page size — the read path is public and cached, so an N+1 here would be felt.
    /// </summary>
    private async Task<ArticleReferences> ResolveAsync(
        IReadOnlyCollection<Article> articles, CancellationToken ct)
    {
        if (articles.Count == 0) return ArticleReferences.Empty;

        var authors = await userDirectory.GetUsersAsync(
            articles.Select(a => a.AuthorId).Distinct().ToArray(), ct);

        var categoryIds = articles
            .Select(a => a.CategoryId)
            .OfType<Guid>()
            .Distinct()
            .ToHashSet();

        var categoryMap = categoryIds.Count == 0
            ? new Dictionary<Guid, Category>()
            : (await categories.ListAllAsync(ct))
                .Where(c => categoryIds.Contains(c.Id))
                .ToDictionary(c => c.Id);

        var tagIdsByArticle = await repository.GetTagIdsAsync(
            articles.Select(a => a.Id).ToArray(), ct);

        var allTagIds = tagIdsByArticle.Values.SelectMany(ids => ids).Distinct().ToArray();
        var tagMap = (await tags.GetByIdsAsync(allTagIds, ct)).ToDictionary(t => t.Id);

        return new ArticleReferences(authors, categoryMap, tagMap, tagIdsByArticle);
    }
}
