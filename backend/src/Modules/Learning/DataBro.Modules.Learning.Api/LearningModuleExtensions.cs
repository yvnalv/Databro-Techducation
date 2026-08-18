using DataBro.Modules.Learning.Application;
using DataBro.Modules.Learning.Infrastructure;
using DataBro.Platform.Abstractions;
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
        MapLearnerEndpoints(endpoints);
        MapAuthoringEndpoints(endpoints);
        return endpoints;
    }

    // ---- The learner's own progress.
    //
    // Authenticated, but with **no permission requirement** — deliberately. Every other write on the
    // platform is an editorial act gated by RBAC; this is a learner acting on their own data, and
    // minting a `Learning.Enrol` permission would mean every new signup needs it granted before the
    // platform does the thing it exists to do. Being signed in is the entitlement.
    //
    // The authorization that matters here is not the role but the id: the learner is taken from the
    // token and never from the route or body, so there is no request shape that reads or writes
    // someone else's progress. That is why these live under /me rather than /users/{id}. ----
    private static void MapLearnerEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/me/enrollments")
            .WithTags("Learning.Progress")
            .RequireAuthorization();

        group.MapGet("", (
            ICurrentUser user, EnrollmentService service, int? page, int? pageSize, CancellationToken ct) =>
            RequireUser(user, async id =>
                ApiEnvelope.OkPaged(await service.ListForUserAsync(id, new PageRequest(page, pageSize), ct))));

        group.MapGet("/{courseSlug}", (
            string courseSlug, ICurrentUser user, EnrollmentService service, CancellationToken ct) =>
            RequireUser(user, async id =>
                ApiEnvelope.OkOrNotFound(await service.GetAsync(id, courseSlug, ct))));

        group.MapPost("/{courseSlug}", (
            string courseSlug, ICurrentUser user, EnrollmentService service, CancellationToken ct) =>
            RequireUser(user, async id =>
                ApiEnvelope.From(await service.EnrolAsync(id, courseSlug, ct))));

        // Moving the resume point. Separate from completion because opening a lesson and finishing
        // it are different claims, and conflating them would mark a course complete for someone who
        // merely scrolled to the end of it.
        group.MapPost("/{courseSlug}/lessons/{lessonId:guid}/visit", (
            string courseSlug, Guid lessonId, ICurrentUser user, EnrollmentService service, CancellationToken ct) =>
            RequireUser(user, async id =>
                ApiEnvelope.From(await service.VisitLessonAsync(id, courseSlug, lessonId, ct))));

        group.MapPost("/{courseSlug}/lessons/{lessonId:guid}/complete", (
            string courseSlug, Guid lessonId, ICurrentUser user, EnrollmentService service, CancellationToken ct) =>
            RequireUser(user, async id =>
                ApiEnvelope.From(await service.CompleteLessonAsync(id, courseSlug, lessonId, ct))));

        group.MapDelete("/{courseSlug}/lessons/{lessonId:guid}/complete", (
            string courseSlug, Guid lessonId, ICurrentUser user, EnrollmentService service, CancellationToken ct) =>
            RequireUser(user, async id =>
                ApiEnvelope.From(await service.ReopenLessonAsync(id, courseSlug, lessonId, ct))));
    }

    /// <summary>
    /// Unwraps the authenticated learner's id. <c>RequireAuthorization</c> has already rejected
    /// anonymous callers, so a missing id means a token that authenticated without a usable subject
    /// — a 401, not a crash and not a silent read of nobody's progress.
    /// </summary>
    private static Task<IResult> RequireUser(ICurrentUser user, Func<Guid, Task<IResult>> handler) =>
        user.UserId is { } id ? handler(id) : Task.FromResult(Results.Unauthorized());

    // ---- Public read. Only published courses, and within them only published lessons (ADR-0013). ----
    private static void MapPublicEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/courses").WithTags("Learning");

        group.MapGet("", async (CourseService service, int? page, int? pageSize, CancellationToken ct) =>
            ApiEnvelope.OkPaged(await service.ListPublishedAsync(new PageRequest(page, pageSize), ct)));

        group.MapGet("/{slug}", async (string slug, CourseService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetPublishedBySlugAsync(slug, ct)));

        // ---- Learning paths ----

        var pathGroup = endpoints.MapGroup("/api/v1/learning-paths").WithTags("Learning");

        pathGroup.MapGet("", async (
            LearningPathService service, int? page, int? pageSize, CancellationToken ct) =>
            ApiEnvelope.OkPaged(await service.ListPublishedAsync(new PageRequest(page, pageSize), ct)));

        pathGroup.MapGet("/{slug}", async (
            string slug, LearningPathService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetPublishedBySlugAsync(slug, ct)));

        // A lesson page. Nested under its course because that is what gives it prev/next, a
        // breadcrumb and a progress context — the same body reached through two courses is two
        // different positions in two different sequences, and the URL should say which.
        group.MapGet("/{slug}/lessons/{lessonSlug}", async (
            string slug, string lessonSlug, CourseService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetLessonPageAsync(slug, lessonSlug, ct)));
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

        MapPathAuthoringEndpoints(endpoints);
    }

    // ---- Learning-path authoring. Same permission split as courses: curating is editing, and
    // putting a path live is publishing. ----
    private static void MapPathAuthoringEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/authoring/learning-paths").WithTags("Learning.Authoring");

        group.MapPost("", async (
            CreateLearningPathRequest request, LearningPathService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.CreateAsync(request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentCreate));

        group.MapGet("", async (
            LearningPathService service, int? page, int? pageSize, CancellationToken ct) =>
            ApiEnvelope.OkPaged(await service.ListAllAsync(new PageRequest(page, pageSize), ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapGet("/{id:guid}", async (Guid id, LearningPathService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetForAuthoringAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPatch("/{id:guid}", async (
            Guid id, UpdateLearningPathRequest request, LearningPathService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.UpdateAsync(id, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPost("/{id:guid}/courses/{courseId:guid}", async (
            Guid id, Guid courseId, LearningPathService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.AddCourseAsync(id, courseId, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapDelete("/{id:guid}/courses/{courseId:guid}", async (
            Guid id, Guid courseId, LearningPathService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.RemoveCourseAsync(id, courseId, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPut("/{id:guid}/courses/order", async (
            Guid id, ReorderRequest request, LearningPathService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.ReorderCoursesAsync(id, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPost("/{id:guid}/publish", async (
            Guid id, LearningPathService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.PublishAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentPublish));

        group.MapPost("/{id:guid}/unpublish", async (
            Guid id, LearningPathService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.UnpublishAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentPublish));
    }

    private static string Perm(string permission) => $"perm:{permission}";
}
