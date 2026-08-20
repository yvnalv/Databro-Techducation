using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DataBro.Modules.Identity.Application;
using DataBro.Platform.Results;
using Microsoft.Extensions.Options;

namespace DataBro.Modules.Identity.Infrastructure.Auth.External;

/// <summary>
/// Google sign-in (ADR-0019). One userinfo call settles identity: Google returns <c>email</c> and
/// <c>email_verified</c> together, so unlike GitHub there is no second request to find a usable
/// address.
/// </summary>
public sealed class GoogleProvider(
    IHttpClientFactory httpClientFactory, IOptions<GoogleOAuthOptions> options) : IExternalIdentityProvider
{
    private const string AuthorizeEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";

    private readonly GoogleOAuthOptions _options = options.Value;

    public string Name => "google";

    public string BuildAuthorizeUrl(string redirectUri, string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["access_type"] = "online",
            // Let a signed-in-elsewhere person choose which Google account, rather than being silently
            // taken through whichever one their browser last used.
            ["prompt"] = "select_account",
        };

        return QueryHelpers_AddQueryString(AuthorizeEndpoint, query);
    }

    public async Task<Result<ExternalUserInfo>> ExchangeCodeAsync(
        string code, string redirectUri, CancellationToken ct = default)
    {
        var http = httpClientFactory.CreateClient();

        var tokenResponse = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
            }), ct);

        if (!tokenResponse.IsSuccessStatusCode)
            return Result.Failure<ExternalUserInfo>(ExchangeFailed);

        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenBody>(ct);
        if (token is null || string.IsNullOrEmpty(token.AccessToken))
            return Result.Failure<ExternalUserInfo>(ExchangeFailed);

        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
        request.Headers.Authorization = new("Bearer", token.AccessToken);

        var userResponse = await http.SendAsync(request, ct);
        if (!userResponse.IsSuccessStatusCode)
            return Result.Failure<ExternalUserInfo>(ExchangeFailed);

        var user = await userResponse.Content.ReadFromJsonAsync<UserInfoBody>(ct);
        if (user is null || string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrEmpty(user.Sub))
            return Result.Failure<ExternalUserInfo>(ExchangeFailed);

        return Result.Success(new ExternalUserInfo(
            Provider: Name,
            ProviderKey: user.Sub,
            Email: user.Email,
            EmailVerified: user.EmailVerified,
            DisplayName: user.Name ?? string.Empty));
    }

    private static readonly Error ExchangeFailed =
        new("unauthenticated", "Google sign-in could not be completed.");

    private sealed record TokenBody(
        [property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record UserInfoBody(
        [property: JsonPropertyName("sub")] string? Sub,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("email_verified")] bool EmailVerified,
        [property: JsonPropertyName("name")] string? Name);

    // Minimal query-string builder so the provider carries no extra dependency.
    internal static string QueryHelpers_AddQueryString(string uri, IEnumerable<KeyValuePair<string, string?>> query)
    {
        var parts = query
            .Where(kv => kv.Value is not null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");
        return $"{uri}?{string.Join('&', parts)}";
    }
}
