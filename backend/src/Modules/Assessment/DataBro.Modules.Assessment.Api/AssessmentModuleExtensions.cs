using DataBro.Modules.Assessment.Application;
using DataBro.Modules.Assessment.Infrastructure;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Authorization;
using DataBro.Platform.Results;
using DataBro.Platform.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataBro.Modules.Assessment.Api;

/// <summary>Composition root for the Assessment module: DI registration and endpoint mapping.</summary>
public static class AssessmentModuleExtensions
{
    public static IServiceCollection AddAssessmentModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAssessmentInfrastructure(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapAssessmentModule(this IEndpointRouteBuilder endpoints)
    {
        MapLearnerEndpoints(endpoints);
        MapAuthoringEndpoints(endpoints);
        return endpoints;
    }

    // ---- The learner's side.
    //
    // Authenticated with no permission requirement, like progress: taking a quiz is a learner acting
    // on their own data, and being signed in is the entitlement.
    //
    // **Nothing reachable here carries the answer key** until an attempt is submitted. That is
    // enforced by the DTO types rather than by remembering — `ChoiceDto` has no correctness field to
    // populate. ----
    private static void MapLearnerEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/lessons/{lessonId:guid}/quiz")
            .WithTags("Assessment")
            .RequireAuthorization();

        // The quiz itself, questions and choices, no answers. Authenticated rather than public: a
        // question bank is worth something, and there is no SEO case for exposing one.
        group.MapGet("", async (Guid lessonId, QuizService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetForLessonAsync(lessonId, ct)));

        group.MapPost("/attempts", (
            Guid lessonId, ICurrentUser user, AttemptService service, CancellationToken ct) =>
            RequireUser(user, async id =>
                ApiEnvelope.From(await service.StartAsync(id, lessonId, ct))));

        group.MapGet("/attempts", (
            Guid lessonId, ICurrentUser user, AttemptService service, CancellationToken ct) =>
            RequireUser(user, async id =>
                ApiEnvelope.Ok(await service.ListForLessonAsync(id, lessonId, ct))));

        var attempts = endpoints
            .MapGroup("/api/v1/me/attempts")
            .WithTags("Assessment")
            .RequireAuthorization();

        attempts.MapGet("/{attemptId:guid}", (
            Guid attemptId, ICurrentUser user, AttemptService service, CancellationToken ct) =>
            RequireUser(user, async id =>
                ApiEnvelope.OkOrNotFound(await service.GetAsync(id, attemptId, ct))));

        // The submission carries selections only. Scoring happens in the domain from the stored
        // answer key — a score arriving over the wire would make the quiz an honour system.
        attempts.MapPost("/{attemptId:guid}/submit", (
            Guid attemptId, SubmitAttemptRequest request, ICurrentUser user,
            AttemptService service, CancellationToken ct) =>
            RequireUser(user, async id =>
                ApiEnvelope.From(await service.SubmitAsync(id, attemptId, request, ct))));
    }

    /// <summary>
    /// Unwraps the authenticated learner's id. <c>RequireAuthorization</c> has already rejected
    /// anonymous callers, so a missing id means a token that authenticated without a usable subject.
    /// </summary>
    private static Task<IResult> RequireUser(ICurrentUser user, Func<Guid, Task<IResult>> handler) =>
        user.UserId is { } id ? handler(id) : Task.FromResult(Results.Unauthorized());

    // ---- Authoring. Writing a quiz is a content-editing act and publishing one is a publishing act,
    // the same split articles and courses use (CT-4). ----
    private static void MapAuthoringEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/authoring/quizzes").WithTags("Assessment.Authoring");

        group.MapPost("", async (CreateQuizRequest request, QuizService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.CreateAsync(request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentCreate));

        group.MapGet("", async (QuizService service, int? page, int? pageSize, CancellationToken ct) =>
            ApiEnvelope.OkPaged(await service.ListAllAsync(new PageRequest(page, pageSize), ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapGet("/{id:guid}", async (Guid id, QuizService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetForAuthoringAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPatch("/{id:guid}", async (
            Guid id, UpdateQuizRequest request, QuizService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.UpdateAsync(id, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        // ---- Questions ----

        group.MapPost("/{id:guid}/questions", async (
            Guid id, AddQuestionRequest request, QuizService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.AddQuestionAsync(id, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPatch("/{id:guid}/questions/{questionId:guid}", async (
            Guid id, Guid questionId, UpdateQuestionRequest request,
            QuizService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.UpdateQuestionAsync(id, questionId, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapDelete("/{id:guid}/questions/{questionId:guid}", async (
            Guid id, Guid questionId, QuizService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.RemoveQuestionAsync(id, questionId, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapPut("/{id:guid}/questions/order", async (
            Guid id, ReorderRequest request, QuizService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.ReorderQuestionsAsync(id, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        // ---- Choices ----

        group.MapPost("/{id:guid}/questions/{questionId:guid}/choices", async (
            Guid id, Guid questionId, AddChoiceRequest request, QuizService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.AddChoiceAsync(id, questionId, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        group.MapDelete("/{id:guid}/questions/{questionId:guid}/choices/{choiceId:guid}", async (
            Guid id, Guid questionId, Guid choiceId, QuizService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.RemoveChoiceAsync(id, questionId, choiceId, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        // The answer key is set as a whole rather than toggled per choice, so "two correct answers
        // on a single-choice question" is not a state the author can pass through.
        group.MapPut("/{id:guid}/questions/{questionId:guid}/answer", async (
            Guid id, Guid questionId, SetCorrectChoicesRequest request,
            QuizService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.SetCorrectChoicesAsync(id, questionId, request, ct)))
            .RequireAuthorization(Perm(Permissions.ContentEdit));

        // ---- Publishing ----

        group.MapPost("/{id:guid}/publish", async (Guid id, QuizService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.PublishAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentPublish));

        group.MapPost("/{id:guid}/unpublish", async (Guid id, QuizService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.UnpublishAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentPublish));
    }

    private static string Perm(string permission) => $"perm:{permission}";
}
