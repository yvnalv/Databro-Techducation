using DataBro.Modules.Content.Application;
using DataBro.Modules.Content.Infrastructure;
using DataBro.Platform.Authorization;
using DataBro.Platform.Results;
using DataBro.Platform.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Api;

/// <summary>Composition root for the Content module: DI registration and endpoint mapping.</summary>
public static class ContentModuleExtensions
{
    public static IServiceCollection AddContentModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddContentInfrastructure(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapContentModule(this IEndpointRouteBuilder endpoints)
    {
        MapPublicEndpoints(endpoints);
        MapSearchEndpoint(endpoints);
        MapPublicTaxonomyEndpoints(endpoints);
        MapRedirectEndpoints(endpoints);
        MapAuthoringEndpoints(endpoints);
        MapTaxonomyAuthoringEndpoints(endpoints);
        return endpoints;
    }

    // ---- Redirect lookup. The `site` app hits this on a 404 to see whether a moved slug should
    // resolve to a 301 instead of a dead page (docs/SEO.md §4). Public and cacheable. ----
    private static void MapRedirectEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/redirects", async (
            string? from, RedirectService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(from))
                return ApiEnvelope.OkOrNotFound(null);

            return ApiEnvelope.OkOrNotFound(await service.ResolveAsync(from, ct));
        }).WithTags("Content");
    }

    // ---- Public read surface (docs/API_SPEC.md §5). Serves only published content. ----
    private static void MapPublicEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/articles").WithTags("Content");

        // Offset-paged rather than cursor-paged: these listings are indexable, and a crawler needs
        // stable page URLs it can enumerate (docs/SEO.md; deviation noted in API_SPEC §3).
        group.MapGet("", async (
            ArticleService service,
            TaxonomyService taxonomy,
            int? page,
            int? pageSize,
            string? category,
            string? tag,
            CancellationToken ct) =>
        {
            var filters = await ResolveFiltersAsync(taxonomy, category, tag, ct);
            if (filters is null)
                return ApiEnvelope.OkPaged(PagedResult<ArticleSummaryDto>.Empty(page ?? 1, pageSize ?? PageRequest.DefaultPageSize));

            var (categoryId, tagId) = filters.Value;
            return ApiEnvelope.OkPaged(
                await service.ListPublishedAsync(new PageRequest(page, pageSize), categoryId, tagId, ct));
        });

        group.MapGet("/{slug}", async (string slug, ArticleService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetPublishedBySlugAsync(slug, ct)));
    }

    // ---- Search. Mapped by Content rather than the Search module for Phase 1 (ADR-0010): the
    // index is a generated column on `articles`, so it lives with the data it is generated from.
    // `/api/v1/search` is the seam that must survive the move to OpenSearch, not this file. ----
    private static void MapSearchEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/search", async (
            string? q,
            string? locale,
            int? page,
            int? pageSize,
            ArticleService service,
            CancellationToken ct) =>
        {
            var result = await service.SearchAsync(q, locale, new PageRequest(page, pageSize), ct);

            // matchMode rides in `meta` rather than wrapping the array, so the response shape stays
            // identical to every other paged listing and the client reuses one parser.
            return ApiEnvelope.OkPaged(
                result.Results,
                new Dictionary<string, object?> { ["matchMode"] = result.MatchMode });
        }).WithTags("Content.Search");
    }

    // ---- Public taxonomy reads. Cacheable and small; drives site navigation. ----
    private static void MapPublicTaxonomyEndpoints(IEndpointRouteBuilder endpoints)
    {
        var categories = endpoints.MapGroup("/api/v1/categories").WithTags("Content.Taxonomy");

        categories.MapGet("", async (TaxonomyService service, CancellationToken ct) =>
            ApiEnvelope.Ok(await service.ListCategoriesAsync(ct)));

        categories.MapGet("/{slug}", async (string slug, TaxonomyService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetCategoryBySlugAsync(slug, ct)));

        var tags = endpoints.MapGroup("/api/v1/tags").WithTags("Content.Taxonomy");

        tags.MapGet("", async (TaxonomyService service, CancellationToken ct) =>
            ApiEnvelope.Ok(await service.ListTagsAsync(ct)));

        tags.MapGet("/{slug}", async (string slug, TaxonomyService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetTagBySlugAsync(slug, ct)));
    }

    // ---- Authoring surface. Requires RBAC permissions (docs/SECURITY.md): authors draft/edit,
    // editors publish. Authorization is enforced via perm:{Permission} policies. ----
    private static void MapAuthoringEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/authoring/articles").WithTags("Content.Authoring");

        group.MapPost("", async (CreateArticleRequest request, ArticleService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.CreateDraftAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<CreateArticleRequest>>()
            .RequireAuthorization(Perm(Permissions.ContentCreate));

        group.MapGet("", async (ArticleService service, int? page, int? pageSize, CancellationToken ct) =>
            ApiEnvelope.OkPaged(await service.ListAllAsync(new PageRequest(page, pageSize), ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapGet("/{id:guid}", async (Guid id, ArticleService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetByIdAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPatch("/{id:guid}", async (Guid id, UpdateArticleRequest request, ArticleService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.UpdateDraftAsync(id, request, ct)))
            .AddEndpointFilter<ValidationFilter<UpdateArticleRequest>>()
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPost("/{id:guid}/publish", async (Guid id, ArticleService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.PublishAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentPublish));

        group.MapPost("/{id:guid}/unpublish", async (Guid id, ArticleService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.UnpublishAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentPublish));

        // Scheduling is a publishing act (CT-4/CT-7): the background sweep publishes it when due.
        group.MapPost("/{id:guid}/schedule", async (Guid id, ScheduleArticleRequest request, ArticleService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.ScheduleAsync(id, request.ScheduledFor, ct)))
            .AddEndpointFilter<ValidationFilter<ScheduleArticleRequest>>()
            .RequireAuthorization(Perm(Permissions.ContentPublish));

        // Cancelling a schedule is the counterpart to setting one (CT-7). Without it, scheduling was
        // a one-way door: unpublish only accepts a published article, so an editor who changed their
        // mind about next Tuesday had no way back to draft.
        group.MapPost("/{id:guid}/unschedule", async (Guid id, ArticleService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.CancelScheduleAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentPublish));

        // ---- Version history (CT-8). Reading and restoring are both *draft* operations, so they
        // sit behind Content.Edit rather than Content.Publish: restoring copies a snapshot into the
        // draft and changes nothing a reader sees until someone publishes afterwards. ----
        group.MapGet("/{id:guid}/versions", async (Guid id, ArticleService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.ListVersionsAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapGet("/{id:guid}/versions/{version:int}", async (
            Guid id, int version, ArticleService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetVersionAsync(id, version, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPost("/{id:guid}/versions/{version:int}/restore", async (
            Guid id, int version, ArticleService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.RestoreVersionAsync(id, version, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        // Changing a public URL is a publishing concern, not a drafting one (CT-3): behind
        // Content.Publish, alongside a 301 the service writes for an already-published article.
        group.MapPut("/{id:guid}/slug", async (Guid id, ChangeSlugRequest request, ArticleService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.ChangeSlugAsync(id, request.Slug, ct)))
            .AddEndpointFilter<ValidationFilter<ChangeSlugRequest>>()
            .RequireAuthorization(Perm(Permissions.ContentPublish));
    }

    // ---- Taxonomy authoring. Behind Taxonomy.Manage (Editor/Admin), which an Author deliberately
    // lacks: an Author may assign existing terms while editing an article, but cannot mint new
    // ones — that is what keeps tag vocabulary from sprawling. ----
    private static void MapTaxonomyAuthoringEndpoints(IEndpointRouteBuilder endpoints)
    {
        var categories = endpoints.MapGroup("/api/v1/authoring/categories").WithTags("Content.Taxonomy");

        categories.MapPost("", async (CreateCategoryRequest request, TaxonomyService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.CreateCategoryAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<CreateCategoryRequest>>()
            .RequireAuthorization(Perm(Permissions.TaxonomyManage));

        categories.MapPatch("/{id:guid}", async (Guid id, UpdateCategoryRequest request, TaxonomyService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.UpdateCategoryAsync(id, request, ct)))
            .AddEndpointFilter<ValidationFilter<UpdateCategoryRequest>>()
            .RequireAuthorization(Perm(Permissions.TaxonomyManage));

        categories.MapDelete("/{id:guid}", async (Guid id, TaxonomyService service, CancellationToken ct) =>
            ApiEnvelope.FromEmpty(await service.DeleteCategoryAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.TaxonomyManage));

        categories.MapPut("/{id:guid}/slug", async (Guid id, ChangeSlugRequest request, TaxonomyService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.ChangeCategorySlugAsync(id, request.Slug, ct)))
            .AddEndpointFilter<ValidationFilter<ChangeSlugRequest>>()
            .RequireAuthorization(Perm(Permissions.TaxonomyManage));

        var tags = endpoints.MapGroup("/api/v1/authoring/tags").WithTags("Content.Taxonomy");

        tags.MapPost("", async (CreateTagRequest request, TaxonomyService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.CreateTagAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<CreateTagRequest>>()
            .RequireAuthorization(Perm(Permissions.TaxonomyManage));

        tags.MapPatch("/{id:guid}", async (Guid id, UpdateTagRequest request, TaxonomyService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.UpdateTagAsync(id, request, ct)))
            .AddEndpointFilter<ValidationFilter<UpdateTagRequest>>()
            .RequireAuthorization(Perm(Permissions.TaxonomyManage));

        tags.MapDelete("/{id:guid}", async (Guid id, TaxonomyService service, CancellationToken ct) =>
            ApiEnvelope.FromEmpty(await service.DeleteTagAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.TaxonomyManage));

        tags.MapPut("/{id:guid}/slug", async (Guid id, ChangeSlugRequest request, TaxonomyService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.ChangeTagSlugAsync(id, request.Slug, ct)))
            .AddEndpointFilter<ValidationFilter<ChangeSlugRequest>>()
            .RequireAuthorization(Perm(Permissions.TaxonomyManage));
    }

    /// <summary>
    /// Translates public <c>?category=</c> / <c>?tag=</c> slugs into ids. Returns null when a slug
    /// was supplied but matches nothing, so the endpoint answers with an empty page rather than
    /// silently ignoring the filter and serving every article.
    /// </summary>
    private static async Task<(Guid? CategoryId, Guid? TagId)?> ResolveFiltersAsync(
        TaxonomyService taxonomy, string? categorySlug, string? tagSlug, CancellationToken ct)
    {
        Guid? categoryId = null;
        Guid? tagId = null;

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            var category = await taxonomy.GetCategoryBySlugAsync(categorySlug, ct);
            if (category is null) return null;
            categoryId = category.Category.Id;
        }

        if (!string.IsNullOrWhiteSpace(tagSlug))
        {
            var tag = await taxonomy.GetTagBySlugAsync(tagSlug, ct);
            if (tag is null) return null;
            tagId = tag.Id;
        }

        return (categoryId, tagId);
    }

    private static string Perm(string permission) => $"perm:{permission}";
}
