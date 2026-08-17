using DataBro.Modules.Learning.Application;
using DataBro.Modules.Learning.Infrastructure;
using DataBro.Platform.Authorization;
using DataBro.Platform.Results;
using DataBro.Platform.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataBro.Modules.Learning.Api;

/// <summary>Composition root for the Learning module: DI registration and endpoint mapping.</summary>
public static class LearningModuleExtensions
{
    public static IServiceCollection AddLearningModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddLearningInfrastructure(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapLearningModule(this IEndpointRouteBuilder endpoints)
    {
        MapPublicEndpoints(endpoints);
        MapAuthoringEndpoints(endpoints);
        return endpoints;
    }

    // ---- Public read. Only published courses, and within them only published lessons (ADR-0013). ----
    private static void MapPublicEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/courses").WithTags("Learning");

        group.MapGet("", async (CourseService service, int? page, int? pageSize, CancellationToken ct) =>
            ApiEnvelope.OkPaged(await service.ListPublishedAsync(new PageRequest(page, pageSize), ct)));

        group.MapGet("/{slug}", async (string slug, CourseService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetPublishedBySlugAsync(slug, ct)));
    }

    // ---- Authoring. Curriculum structure is a content-editing act, so it sits behind Content.Edit;
    // publishing a course is a publishing act, behind Content.Publish — the same split articles use
    // (CT-4). Reusing the Content permissions rather than minting Learning ones keeps one editorial
    // role rather than asking an administrator to grant two overlapping sets. ----
    private static void MapAuthoringEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/authoring/courses").WithTags("Learning.Authoring");

        group.MapPost("", async (CreateCourseRequest request, CourseService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.CreateAsync(request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentCreate));

        group.MapGet("", async (CourseService service, int? page, int? pageSize, CancellationToken ct) =>
            ApiEnvelope.OkPaged(await service.ListAllAsync(new PageRequest(page, pageSize), ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapGet("/{id:guid}", async (Guid id, CourseService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetForAuthoringAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPatch("/{id:guid}", async (
            Guid id, UpdateCourseRequest request, CourseService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.UpdateAsync(id, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        // ---- Curriculum structure ----

        group.MapPost("/{id:guid}/modules", async (
            Guid id, AddModuleRequest request, CourseService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.AddModuleAsync(id, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPatch("/{id:guid}/modules/{moduleId:guid}", async (
            Guid id, Guid moduleId, UpdateModuleRequest request, CourseService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.UpdateModuleAsync(id, moduleId, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapDelete("/{id:guid}/modules/{moduleId:guid}", async (
            Guid id, Guid moduleId, CourseService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.RemoveModuleAsync(id, moduleId, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        // A whole rearrangement in one call, because it is one transaction against one aggregate.
        // Sending a move per row would let a drag half-apply and leave the order wrong.
        group.MapPut("/{id:guid}/modules/order", async (
            Guid id, ReorderRequest request, CourseService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.ReorderModulesAsync(id, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPost("/{id:guid}/modules/{moduleId:guid}/lessons", async (
            Guid id, Guid moduleId, AddLessonRequest request, CourseService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.AddLessonAsync(id, moduleId, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPatch("/{id:guid}/modules/{moduleId:guid}/lessons/{lessonId:guid}", async (
            Guid id, Guid moduleId, Guid lessonId, UpdateLessonRequest request,
            CourseService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.UpdateLessonAsync(id, moduleId, lessonId, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapDelete("/{id:guid}/modules/{moduleId:guid}/lessons/{lessonId:guid}", async (
            Guid id, Guid moduleId, Guid lessonId, CourseService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.RemoveLessonAsync(id, moduleId, lessonId, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPut("/{id:guid}/modules/{moduleId:guid}/lessons/order", async (
            Guid id, Guid moduleId, ReorderRequest request, CourseService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.ReorderLessonsAsync(id, moduleId, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        // ---- Publishing ----

        group.MapPost("/{id:guid}/publish", async (Guid id, CourseService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.PublishAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentPublish));

        group.MapPost("/{id:guid}/unpublish", async (Guid id, CourseService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.UnpublishAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentPublish));
    }

    private static string Perm(string permission) => $"perm:{permission}";
}
