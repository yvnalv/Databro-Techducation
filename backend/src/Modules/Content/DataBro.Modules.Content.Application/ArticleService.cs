using DataBro.Modules.Content.Domain;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Results;

namespace DataBro.Modules.Content.Application;

/// <summary>Use cases for the Content module's Article aggregate (thin controllers call this).</summary>
public sealed class ArticleService(
    IArticleRepository repository,
    ICategoryRepository categories,
    ITagRepository tags,
    RedirectService redirects,
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

    /// <summary>
    /// Schedules an article to publish automatically at <paramref name="scheduledFor"/> (CT-7). The
    /// background sweep (<see cref="ScheduledPublishingJob"/>) does the actual publish when the time
    /// arrives.
    /// </summary>
    public async Task<Result<ArticleDto>> ScheduleAsync(Guid id, DateTimeOffset scheduledFor, CancellationToken ct = default)
    {
        var article = await repository.GetByIdAsync(id, ct);
        if (article is null)
            return Result.Failure<ArticleDto>(Error.NotFound("Article not found."));

        var result = article.Schedule(scheduledFor, clock.UtcNow);
        if (result.IsFailure)
            return Result.Failure<ArticleDto>(result.Error);

        await repository.SaveChangesAsync(ct);
        return Result.Success(article.ToDraftDto(await ResolveAsync([article], ct)));
    }

    /// <summary>
    /// Changes an article's slug (CT-2/CT-3). If the article has ever been published its old
    /// <c>/articles/{slug}</c> path is indexed, so a 301 is recorded from it to the new path in the
    /// same transaction; a never-published draft simply moves, since it had no public URL to protect.
    /// </summary>
    public async Task<Result<ArticleDto>> ChangeSlugAsync(Guid id, string newSlug, CancellationToken ct = default)
    {
        var article = await repository.GetByIdAsync(id, ct);
        if (article is null)
            return Result.Failure<ArticleDto>(Error.NotFound("Article not found."));

        Slug slug;
        try
        {
            slug = Slug.Create(newSlug);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<ArticleDto>(Error.Validation(ex.Message));
        }

        if (slug.Equals(article.Slug))
            return Result.Success(article.ToDraftDto(await ResolveAsync([article], ct)));

        if (await repository.SlugExistsAsync(slug.Value, ct))
            return Result.Failure<ArticleDto>(new Error("slug_taken", $"The slug '{slug.Value}' is already in use."));

        var previous = article.ChangeSlug(slug);

        // previous is non-null here (slug differs), but the check keeps the intent explicit.
        if (previous is not null && article.PublishedAt is not null)
            await redirects.RecordAsync(
                ContentPaths.Article(previous.Value), ContentPaths.Article(slug.Value),
                "article slug changed", ct);

        await repository.SaveChangesAsync(ct);
        return Result.Success(article.ToDraftDto(await ResolveAsync([article], ct)));
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

    /// <summary>
    /// Full-text search over published content (ADR-0010), falling back to trigram similarity when
    /// the query matches nothing — which is almost always a typo, and returning an empty page for a
    /// one-character slip is a bad answer when a good one is available.
    /// </summary>
    public async Task<SearchResultDto> SearchAsync(
        string? query, string? locale, PageRequest page, CancellationToken ct = default)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        var scope = NormalizeLocale(locale);

        // A single character matches most of the catalogue under trigram similarity and nothing
        // useful under full-text. Answering "nothing" is more truthful than answering "everything".
        if (trimmed.Length < MinQueryLength)
            return new SearchResultDto(
                PagedResult<ArticleSummaryDto>.Empty(page.Page, page.PageSize),
                SearchMatchModes.Exact);

        var exact = await repository.SearchPublishedAsync(trimmed, scope, page, fuzzy: false, ct);
        if (exact.Total > 0)
            return new SearchResultDto(await ToSummaryPageAsync(exact, ct), SearchMatchModes.Exact);

        var fuzzy = await repository.SearchPublishedAsync(trimmed, scope, page, fuzzy: true, ct);

        // Reported as `exact` when the fallback also found nothing: there is no approximation to
        // apologise for, just no results.
        return new SearchResultDto(
            await ToSummaryPageAsync(fuzzy, ct),
            fuzzy.Total > 0 ? SearchMatchModes.Fuzzy : SearchMatchModes.Exact);
    }

    private const int MinQueryLength = 2;

    /// <summary>
    /// Search is scoped to one locale because the index stems per locale (ADR-0010). Anything
    /// unrecognised falls back to the default rather than erroring — a bad `?locale=` should not
    /// turn a search into a 400.
    /// </summary>
    private static string NormalizeLocale(string? locale)
        => string.Equals(locale, "id", StringComparison.OrdinalIgnoreCase) ? "id" : "en";

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
