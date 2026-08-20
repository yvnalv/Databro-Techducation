using DataBro.Modules.Identity.Application;
using DataBro.Platform.Results;
using Microsoft.Extensions.Options;

namespace DataBro.Modules.Identity.Infrastructure.Auth.External;

/// <summary>
/// Orchestrates the social-login round-trip (ADR-0019): challenge → callback → single-use handoff.
/// Holds the flow so the endpoints stay thin, and fails closed at every step.
/// </summary>
public sealed class ExternalAuthService(
    IEnumerable<IExternalIdentityProvider> providers,
    OAuthStateProtector stateProtector,
    IAuthCodeStore codeStore,
    IAuthService authService,
    IOptions<ExternalAuthOptions> options) : IExternalAuthService
{
    // A challenge can sit in the browser while a person picks an account and types a password. Ten
    // minutes is generous for that and still short enough that a leaked state is not a standing key.
    private static readonly TimeSpan StateMaxAge = TimeSpan.FromMinutes(10);

    private readonly ExternalAuthOptions _options = options.Value;

    public string SignInErrorUrl => _options.LoginErrorUrl;

    private static readonly Error UnknownProvider =
        new("not_found", "Unknown sign-in provider.");

    private static readonly Error CallbackRejected =
        new("unauthenticated", "Sign-in could not be completed.");

    public Result<string> BuildChallengeUrl(string provider, string? returnTo)
    {
        var impl = Resolve(provider);
        if (impl is null) return Result.Failure<string>(UnknownProvider);

        // An unsafe return target is dropped, not rejected: the person still gets signed in and lands
        // on their role's home. Only a target we can vouch for is carried.
        var safeReturn = SafeReturnTarget(returnTo);

        var state = stateProtector.Protect(new OAuthState(
            Provider: impl.Name,
            Nonce: Guid.NewGuid().ToString("N"),
            IssuedAt: DateTimeOffset.UtcNow,
            ReturnTo: safeReturn));

        return Result.Success(impl.BuildAuthorizeUrl(_options.CallbackUrl(impl.Name), state));
    }

    public async Task<Result<string>> HandleCallbackAsync(
        string provider, string code, string state, CancellationToken ct = default)
    {
        var impl = Resolve(provider);
        if (impl is null) return Result.Failure<string>(UnknownProvider);

        var unpacked = stateProtector.Unprotect(state, StateMaxAge);
        // Signature/freshness failure, or a callback whose state was minted for another provider — a
        // cross-provider replay. Either way, refuse.
        if (unpacked is null || !string.Equals(unpacked.Provider, impl.Name, StringComparison.Ordinal))
            return Result.Failure<string>(CallbackRejected);

        var info = await impl.ExchangeCodeAsync(code, _options.CallbackUrl(impl.Name), ct);
        if (info.IsFailure) return Result.Failure<string>(info.Error);

        var tokens = await authService.LinkOrCreateExternalAsync(info.Value, ct);
        if (tokens.IsFailure) return Result.Failure<string>(tokens.Error);

        var handoffCode = await codeStore.IssueAsync(tokens.Value, ct);

        var receiver = GoogleProvider.QueryHelpers_AddQueryString(_options.ReceiverUrl,
            new Dictionary<string, string?>
            {
                ["code"] = handoffCode,
                ["returnTo"] = unpacked.ReturnTo,
            });

        return Result.Success(receiver);
    }

    public async Task<Result<AuthTokens>> RedeemHandoffAsync(string code, CancellationToken ct = default)
    {
        var tokens = await codeStore.RedeemAsync(code, ct);
        return tokens is null
            ? Result.Failure<AuthTokens>(new Error("unauthenticated", "That sign-in link has expired. Try again."))
            : Result.Success(tokens);
    }

    private IExternalIdentityProvider? Resolve(string provider) =>
        providers.FirstOrDefault(p =>
            string.Equals(p.Name, provider, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns <paramref name="returnTo"/> only if it is safe to send someone to after login: a
    /// same-app path, or an absolute URL on the app or the public site. Anything else — an off-origin
    /// absolute URL, a protocol-relative <c>//host</c> — becomes <c>null</c>, which is an open-redirect
    /// guard identical in spirit to the one the password login applies on the client.
    /// </summary>
    private string? SafeReturnTarget(string? returnTo)
    {
        if (string.IsNullOrWhiteSpace(returnTo)) return null;

        if (returnTo.StartsWith('/') && !returnTo.StartsWith("//", StringComparison.Ordinal))
            return returnTo;

        if (!Uri.TryCreate(returnTo, UriKind.Absolute, out var target)) return null;

        foreach (var allowed in new[] { _options.AppBaseUrl, _options.SiteBaseUrl })
        {
            if (Uri.TryCreate(allowed, UriKind.Absolute, out var origin) &&
                string.Equals(target.GetLeftPart(UriPartial.Authority),
                    origin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
                return returnTo;
        }

        return null;
    }
}
