using DataBro.Modules.Media.Application;
using DataBro.Modules.Media.Domain;
using DataBro.Modules.Media.Infrastructure;
using DataBro.Platform.Authorization;
using DataBro.Platform.Results;
using DataBro.Platform.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataBro.Modules.Media.Api;

/// <summary>Composition root for the Media module: DI registration and endpoint mapping.</summary>
public static class MediaModuleExtensions
{
    public static IServiceCollection AddMediaModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMediaInfrastructure(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapMediaModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/media").WithTags("Media");

        // ---- Upload. Behind Media.Upload (Author/Editor/Admin) — this writes bytes to public
        // storage, so it is never anonymous. ----
        group.MapPost("", async (HttpRequest request, MediaService service, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return ApiEnvelope.Fail(Error.Validation("The request must be a multipart form upload."));

            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"] ?? form.Files.FirstOrDefault();

            if (file is null || file.Length == 0)
                return ApiEnvelope.Fail(Error.Validation("No file was uploaded."));

            await using var content = file.OpenReadStream();

            return ApiEnvelope.From(await service.UploadAsync(
                new UploadMediaRequest(content, file.FileName, file.Length, form["altText"]), ct));
        })
            // The framework's own multipart cap, set from the domain limit so the two cannot drift.
            // This rejects an oversized body before it is buffered; MediaService re-checks the size
            // because it must not trust that this filter was applied.
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MediaLimits.MaxBytes))
            .DisableAntiforgery()
            .RequireAuthorization(Perm(Permissions.MediaUpload));

        // ---- Library reads. Also behind Media.Upload: this is the CMS picker, not a public
        // gallery. The *files* are public; the index of them is not. ----
        group.MapGet("", async (MediaService service, int? page, int? pageSize, CancellationToken ct) =>
            ApiEnvelope.OkPaged(await service.ListAsync(new PageRequest(page, pageSize), ct)))
            .RequireAuthorization(Perm(Permissions.MediaUpload));

        group.MapGet("/{id:guid}", async (Guid id, MediaService service, CancellationToken ct) =>
            ApiEnvelope.OkOrNotFound(await service.GetAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.MediaUpload));

        group.MapPatch("/{id:guid}", async (
            Guid id, UpdateMediaRequest request, MediaService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.UpdateAsync(id, request, ct)))
            .RequireAuthorization(Perm(Permissions.MediaUpload));

        // Soft delete: the stored objects stay, because published articles may still reference them.
        group.MapDelete("/{id:guid}", async (Guid id, MediaService service, CancellationToken ct) =>
            ApiEnvelope.FromEmpty(await service.DeleteAsync(id, ct)))
            .RequireAuthorization(Perm(Permissions.ContentDelete));

        return endpoints;
    }

    private static string Perm(string permission) => $"perm:{permission}";
}
