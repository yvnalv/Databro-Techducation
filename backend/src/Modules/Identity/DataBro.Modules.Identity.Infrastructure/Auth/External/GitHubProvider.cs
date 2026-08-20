using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DataBro.Modules.Identity.Application;
using DataBro.Platform.Results;
using Microsoft.Extensions.Options;

namespace DataBro.Modules.Identity.Infrastructure.Auth.External;

/// <summary>
/// GitHub sign-in (ADR-0019). Two calls, not one: <c>/user</c> returns <c>null</c> for email whenever
/// the person keeps it private — common — so the scope requests <c>read:user user:email</c> and a
/// second call to <c>/user/emails</c> finds the primary, verified address. Without it a private-email
/// sign-in would silently create a duplicate instead of linking to the existing account (ID-3).
/// </summary>
public sealed class GitHubProvider(
    IHttpClientFactory httpClientFactory, IOptions<GitHubOAuthOptions> options) : IExternalIdentityProvider
{
    private const string AuthorizeEndpoint = "https://github.com/login/oauth/authorize";
    private const string TokenEndpoint = "https://github.com/login/oauth/access_token";
    private const string UserEndpoint = "https://api.github.com/user";
    private const string EmailsEndpoint = "https://api.github.com/user/emails";

    private readonly GitHubOAuthOptions _options = options.Value;

    public string Name => "github";

    public string BuildAuthorizeUrl(string redirectUri, string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "read:user user:email",
            ["state"] = state,
        };

        return GoogleProvider.QueryHelpers_AddQueryString(AuthorizeEndpoint, query);
    }

    public async Task<Result<ExternalUserInfo>> ExchangeCodeAsync(
        string code, string redirectUri, CancellationToken ct = default)
    {
        var http = httpClientFactory.CreateClient();

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
            }),
        };
        tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var tokenResponse = await http.SendAsync(tokenRequest, ct);
        if (!tokenResponse.IsSuccessStatusCode)
            return Result.Failure<ExternalUserInfo>(ExchangeFailed);

        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenBody>(ct);
        if (token is null || string.IsNullOrEmpty(token.AccessToken))
            return Result.Failure<ExternalUserInfo>(ExchangeFailed);

        var user = await GetAsync<UserBody>(http, UserEndpoint, token.AccessToken, ct);
        if (user is null || user.Id == 0)
            return Result.Failure<ExternalUserInfo>(ExchangeFailed);

        var emails = await GetAsync<List<EmailBody>>(http, EmailsEndpoint, token.AccessToken, ct);
        var primary = SelectPrimaryVerifiedEmail(emails);

        if (primary is null)
            return Result.Failure<ExternalUserInfo>(NoVerifiedEmail);

        return Result.Success(new ExternalUserInfo(
            Provider: Name,
            ProviderKey: user.Id.ToString(),
            Email: primary,
            EmailVerified: true,
            DisplayName: string.IsNullOrWhiteSpace(user.Name) ? user.Login ?? string.Empty : user.Name));
    }

    /// <summary>
    /// The primary, verified address from GitHub's email list, or <c>null</c> if none qualifies. Pure
    /// and internal so the selection rule — the crux of ID-3 for GitHub — is unit-tested without HTTP.
    /// A verified-but-not-primary address is deliberately not a fallback: linking must be by the
    /// address the person has designated, not whichever verified one happens to sort first.
    /// </summary>
    public static string? SelectPrimaryVerifiedEmail(IEnumerable<EmailBody>? emails) =>
        emails?
            .FirstOrDefault(e => e is { Primary: true, Verified: true } && !string.IsNullOrWhiteSpace(e.Email))
            ?.Email;

    private static async Task<T?> GetAsync<T>(HttpClient http, string url, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        // GitHub rejects API requests without a User-Agent.
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("DataBro", "1.0"));

        var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>(ct) : default;
    }

    private static readonly Error ExchangeFailed =
        new("unauthenticated", "GitHub sign-in could not be completed.");

    private static readonly Error NoVerifiedEmail =
        new("validation_failed",
            "Your GitHub account has no verified email we can use. Verify an email on GitHub, or sign in another way.");

    private sealed record TokenBody(
        [property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record UserBody(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("login")] string? Login,
        [property: JsonPropertyName("name")] string? Name);

    public sealed record EmailBody(
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("primary")] bool Primary,
        [property: JsonPropertyName("verified")] bool Verified);
}
