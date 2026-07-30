using DataBro.Modules.Identity.Application;
using DataBro.Modules.Identity.Infrastructure;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Results;
using DataBro.Platform.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataBro.Modules.Identity.Api;

/// <summary>Composition root for the Identity module: DI registration and endpoint mapping.</summary>
public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddIdentityInfrastructure(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/v1/auth").WithTags("Identity");

        auth.MapPost("/register", async (RegisterRequest request, IAuthService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.RegisterAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>();

        auth.MapPost("/login", async (LoginRequest request, IAuthService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.LoginAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<LoginRequest>>();

        auth.MapPost("/refresh", async (RefreshTokenRequest request, IAuthService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.RefreshAsync(request, ct)));

        auth.MapPost("/confirm-email", async (ConfirmEmailRequest request, IAuthService service, CancellationToken ct) =>
            ApiEnvelope.FromEmpty(await service.ConfirmEmailAsync(request, ct)));

        endpoints.MapGet("/api/v1/me", async (ICurrentUser currentUser, IAuthService service, CancellationToken ct) =>
            {
                if (currentUser.UserId is not { } userId)
                    return ApiEnvelope.Fail(new Error("unauthenticated", "Not authenticated."));

                return ApiEnvelope.From(await service.GetProfileAsync(userId, ct));
            })
            .WithTags("Identity")
            .RequireAuthorization();

        return endpoints;
    }
}
