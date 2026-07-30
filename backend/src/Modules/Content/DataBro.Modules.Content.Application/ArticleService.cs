using DataBro.Modules.Content.Domain;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Results;

namespace DataBro.Modules.Content.Application;

/// <summary>Use cases for the Content module's Article aggregate (thin controllers call this).</summary>
public sealed class ArticleService(IArticleRepository repository, IClock clock, ICurrentUser currentUser)
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

        await repository.AddAsync(article, ct);
        await repository.SaveChangesAsync(ct);

        return Result.Success(article.ToDraftDto());
    }

    public async Task<Result<ArticleDto>> UpdateDraftAsync(Guid id, UpdateArticleRequest request, CancellationToken ct = default)
    {
        var article = await repository.GetByIdAsync(id, ct);
        if (article is null)
            return Result.Failure<ArticleDto>(Error.NotFound("Article not found."));

        article.UpdateDraft(request.Title, request.Summary, request.Content.ToDomain(), request.Seo?.ToDomain());
        await repository.SaveChangesAsync(ct);
        return Result.Success(article.ToDraftDto());
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
        return Result.Success(article.ToDto());
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
        return Result.Success(article.ToDraftDto());
    }

    public async Task<ArticleDto?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default)
    {
        var article = await repository.GetPublishedBySlugAsync(slug, ct);
        return article?.ToDto();
    }

    public async Task<ArticleDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var article = await repository.GetByIdAsync(id, ct);
        return article?.ToDraftDto();
    }

    public async Task<IReadOnlyList<ArticleSummaryDto>> ListPublishedAsync(int limit = 20, CancellationToken ct = default)
    {
        var articles = await repository.ListPublishedAsync(limit, ct);
        return articles.Select(a => a.ToSummaryDto()).ToList();
    }

    public async Task<IReadOnlyList<ArticleSummaryDto>> ListAllAsync(int limit = 50, CancellationToken ct = default)
    {
        var articles = await repository.ListAllAsync(limit, ct);
        return articles.Select(a => a.ToSummaryDto()).ToList();
    }
}
