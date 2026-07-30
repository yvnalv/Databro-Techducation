namespace DataBro.Modules.Identity.Infrastructure.Auth;

/// <summary>JWT configuration (bound from the "Jwt" section). The signing key is a secret.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "databro";
    public string Audience { get; set; } = "databro";
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
}
