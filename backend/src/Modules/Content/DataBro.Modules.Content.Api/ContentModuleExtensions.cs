using DataBro.Modules.Content.Application;
using DataBro.Modules.Content.Infrastructure;
using DataBro.Platform.Authorization;
using DataBro.Platform.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        MapAuthoringEndpoints(endpoints);
        return endpoints;
    }

    // ---- Public read surface (docs/API_SPEC.md §5). Serves only published content. ----
    private static void MapPublicEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/articles").WithTags("Content");

        group.MapGet("", async (ArticleService service, int? limit, CancellationToken ct) =>
            ApiEnvelope.Ok(await service.ListPublishedAsync(limit ?? 20, ct)));

        group.MapGet("/{slug}", async (string slug, ArticleService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetPublishedBySlugAsync(slug, ct)));
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

        group.MapGet("", async (ArticleService service, int? limit, CancellationToken ct) =>
            ApiEnvelope.Ok(await service.ListAllAsync(limit ?? 50, ct)))
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
    }

    private static string Perm(string permission) => $"perm:{permission}";
}
