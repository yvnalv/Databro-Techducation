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

        // ---- Account recovery.
        //
        // The first three answer identically whether or not the address belongs to an account.
        // An endpoint that did otherwise would be a membership oracle: anyone could test an address
        // list against it and learn who has an account here. ----

        auth.MapPost("/forgot-password", async (
            ForgotPasswordRequest request, IAuthService service, CancellationToken ct) =>
            ApiEnvelope.FromEmpty(await service.ForgotPasswordAsync(request, ct)));

        auth.MapPost("/reset-password", async (
            ResetPasswordRequest request, IAuthService service, CancellationToken ct) =>
            ApiEnvelope.FromEmpty(await service.ResetPasswordAsync(request, ct)));

        auth.MapPost("/resend-confirmation", async (
            ResendConfirmationRequest request, IAuthService service, CancellationToken ct) =>
            ApiEnvelope.FromEmpty(await service.ResendConfirmationAsync(request, ct)));

        // Unauthenticated on purpose: the refresh token in the body is the thing being revoked, and
        // requiring a valid *access* token would make signing out impossible once one had expired —
        // exactly when someone most wants to.
        auth.MapPost("/logout", async (
            LogoutRequest request, IAuthService service, CancellationToken ct) =>
            ApiEnvelope.FromEmpty(await service.LogoutAsync(request, ct)));

        // ---- Social login (ADR-0019).
        //
        // Two of these three are browser navigations, not API calls: they must *redirect*, never
        // return a JSON envelope, or a person mid-sign-in sees a payload instead of a page. Only the
        // handoff exchange is a real API call the app makes, and it alone speaks the envelope. ----

        // Start: redirect the browser to the provider, carrying signed state.
        auth.MapGet("/oauth/{provider}", (
            string provider, string? returnTo, IExternalAuthService service) =>
        {
            var challenge = service.BuildChallengeUrl(provider, returnTo);
            return Results.Redirect(challenge.IsSuccess ? challenge.Value : service.SignInErrorUrl);
        });

        // Callback: the provider sends the browser here. Whatever happens, we redirect — to the app
        // with a single-use handoff code on success, or back to sign-in with an error flag otherwise.
        // A provider that denies consent returns `error` and no code; that too is a redirect home.
        auth.MapGet("/oauth/{provider}/callback", async (
            string provider, string? code, string? state, string? error,
            IExternalAuthService service, CancellationToken ct) =>
        {
            if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                return Results.Redirect(service.SignInErrorUrl);

            var result = await service.HandleCallbackAsync(provider, code, state, ct);
            return Results.Redirect(result.IsSuccess ? result.Value : service.SignInErrorUrl);
        });

        // Exchange: the app POSTs the single-use handoff code and gets the token pair. The one step
        // that is a genuine API call, so the one that returns the envelope.
        auth.MapPost("/oauth/exchange", async (
            ExchangeHandoffRequest request, IExternalAuthService service, CancellationToken ct) =>
            ApiEnvelope.From(await service.RedeemHandoffAsync(request.Code, ct)));

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
