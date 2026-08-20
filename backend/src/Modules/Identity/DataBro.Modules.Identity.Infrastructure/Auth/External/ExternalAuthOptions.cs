namespace DataBro.Modules.Identity.Infrastructure.Auth.External;

/// <summary>
/// Non-secret configuration for the social-login round-trip (ADR-0019), bound from the
/// <c>ExternalAuth</c> section. The provider client IDs and secrets are deliberately <b>not</b> here:
/// they arrive as flat environment variables (<see cref="GoogleOAuthOptions"/> /
/// <see cref="GitHubOAuthOptions"/>) so a secret never sits in a config file.
/// </summary>
public sealed class ExternalAuthOptions
{
    public const string SectionName = "ExternalAuth";

    /// <summary>The browser-reachable base URL of this API — the origin of the OAuth callback URL.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>The learner/authoring app's base URL — where the handoff lands and errors return.</summary>
    public string AppBaseUrl { get; set; } = string.Empty;

    /// <summary>The public site's base URL — the second allowed origin for a post-login return target.</summary>
    public string SiteBaseUrl { get; set; } = string.Empty;

    /// <summary>How long a single-use handoff code lives before it expires. Seconds are plenty.</summary>
    public int HandoffCodeTtlSeconds { get; set; } = 60;

    /// <summary>The app route the callback redirects to with the handoff code.</summary>
    public string ReceiverUrl => $"{AppBaseUrl.TrimEnd('/')}/auth/callback";

    /// <summary>Where a failed social sign-in sends the person: back to the app's sign-in page.</summary>
    public string LoginErrorUrl => $"{AppBaseUrl.TrimEnd('/')}/login?error=oauth";

    /// <summary>The callback URL registered with a provider, per provider name.</summary>
    public string CallbackUrl(string provider) =>
        $"{ApiBaseUrl.TrimEnd('/')}/api/v1/auth/oauth/{provider}/callback";
}

/// <summary>Google OAuth client credentials, bound from <c>GOOGLE_CLIENT_ID</c> / <c>GOOGLE_CLIENT_SECRET</c>.</summary>
public sealed class GoogleOAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>GitHub OAuth app credentials, bound from <c>GITHUB_CLIENT_ID</c> / <c>GITHUB_CLIENT_SECRET</c>.</summary>
public sealed class GitHubOAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
