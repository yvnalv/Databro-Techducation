using DataBro.Platform.Results;

namespace DataBro.Modules.Identity.Application;

/// <summary>Authentication use cases. Implemented in Infrastructure over ASP.NET Core Identity.</summary>
public interface IAuthService
{
    Task<Result<UserProfileDto>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<Result<AuthTokens>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<AuthTokens>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default);
    Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Outbound email port (provider-abstracted). No-op logger until a transport is wired.</summary>
public interface IEmailSender
{
    Task SendEmailConfirmationAsync(string email, Guid userId, string token, CancellationToken ct = default);
}
