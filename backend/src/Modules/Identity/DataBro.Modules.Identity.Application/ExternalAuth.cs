using DataBro.Platform.Results;

namespace DataBro.Modules.Identity.Application;

/// <summary>
/// What a provider vouches for about a person, reduced to the only fields identity depends on
/// (ADR-0019). The provider — Google or GitHub — has already done the authenticating; this is its
/// answer, normalised so <see cref="IAuthService.LinkOrCreateExternalAsync"/> never sees an HTTP body.
/// </summary>
/// <param name="Provider">Lowercase provider name, e.g. <c>google</c> or <c>github</c>.</param>
/// <param name="ProviderKey">The provider's stable id for this user (Google <c>sub</c>, GitHub id).</param>
/// <param name="Email">The address the provider returned.</param>
/// <param name="EmailVerified">
/// Whether the provider says it has verified that address. Linking on an unverified address is an
/// account-takeover primitive (ID-3), so a <c>false</c> here is refused, not trusted.
/// </param>
/// <param name="DisplayName">A human name for a freshly created account; falls back to the local part.</param>
public sealed record ExternalUserInfo(
    string Provider,
    string ProviderKey,
    string Email,
    bool EmailVerified,
    string DisplayName);

/// <summary>
/// One external identity provider (ADR-0019). Rule 14's abstraction applied to authentication: the
/// orchestration sees this port, never a provider's HTTP surface, so a second provider is a class,
/// not a branch.
/// </summary>
public interface IExternalIdentityProvider
{
    /// <summary>Lowercase provider name matched against the <c>{provider}</c> route segment.</summary>
    string Name { get; }

    /// <summary>
    /// The provider's authorization URL to redirect the browser to, carrying our signed
    /// <paramref name="state"/> and the <paramref name="redirectUri"/> the provider will call back.
    /// </summary>
    string BuildAuthorizeUrl(string redirectUri, string state);

    /// <summary>
    /// Exchanges the authorization <paramref name="code"/> for the user's verified identity. Fails
    /// when the exchange is rejected or — for GitHub — no verified primary email is available, which
    /// must refuse the sign-in rather than silently create a duplicate account.
    /// </summary>
    Task<Result<ExternalUserInfo>> ExchangeCodeAsync(
        string code, string redirectUri, CancellationToken ct = default);
}

/// <summary>
/// Orchestrates the social-login round-trip (ADR-0019). Endpoints stay thin: they translate its
/// results into redirects and an envelope, and hold none of the flow themselves.
/// </summary>
public interface IExternalAuthService
{
    /// <summary>
    /// Builds the provider authorization URL to redirect to, embedding signed state that carries a
    /// validated <paramref name="returnTo"/>. Fails for an unknown provider or a disallowed return
    /// target.
    /// </summary>
    Result<string> BuildChallengeUrl(string provider, string? returnTo);

    /// <summary>
    /// Handles the provider callback: verifies state, exchanges the code, links or creates the user,
    /// and returns the app receiver URL carrying a single-use handoff code. Fails closed — every
    /// failure is a reason to send the person back to sign-in, never to leak which step broke.
    /// </summary>
    Task<Result<string>> HandleCallbackAsync(
        string provider, string code, string state, CancellationToken ct = default);

    /// <summary>Redeems a single-use handoff code for the issued token pair. Fails if unknown, used, or expired.</summary>
    Task<Result<AuthTokens>> RedeemHandoffAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Where the browser-facing endpoints send someone when a sign-in cannot be completed: the app's
    /// sign-in page with an error flag. A person mid-redirect must land on a page, never a JSON error.
    /// </summary>
    string SignInErrorUrl { get; }
}

/// <summary>Body of the handoff exchange: the single-use code the app receiver was redirected with.</summary>
public sealed record ExchangeHandoffRequest(string Code);
