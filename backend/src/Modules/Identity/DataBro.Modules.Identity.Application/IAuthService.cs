using DataBro.Platform.Results;

namespace DataBro.Modules.Identity.Application;

/// <summary>Authentication use cases. Implemented in Infrastructure over ASP.NET Core Identity.</summary>
public interface IAuthService
{
    Task<Result<UserProfileDto>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<Result<AuthTokens>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<AuthTokens>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sends a reset link if the address belongs to an account. <b>Always succeeds</b> — see the
    /// implementation for why the answer must not depend on whether the account exists.
    /// </summary>
    Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);

    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);

    /// <summary>Re-sends the confirmation email. Always succeeds, for the same reason.</summary>
    Task<Result> ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken ct = default);

    /// <summary>Revokes one refresh token. Always succeeds — signing out must never fail.</summary>
    Task<Result> LogoutAsync(LogoutRequest request, CancellationToken ct = default);
    Task<Result<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// The emails Identity sends, named for what they mean rather than for how they travel.
///
/// <para>
/// Deliberately kept as its own port rather than having <c>AuthService</c> compose an
/// <c>EmailMessage</c> directly. Identity knows what a verification email <i>is</i>; the transport in
/// <c>Platform.Email</c> knows only how to put a message on the wire. Collapsing the two would put
/// subject lines and a token URL inside an authentication service.
/// </para>
/// </summary>
public interface IIdentityEmails
{
    Task SendEmailConfirmationAsync(
        string email, string displayName, Guid userId, string token, CancellationToken ct = default);

    Task SendPasswordResetAsync(
        string email, string displayName, Guid userId, string token, CancellationToken ct = default);
}
