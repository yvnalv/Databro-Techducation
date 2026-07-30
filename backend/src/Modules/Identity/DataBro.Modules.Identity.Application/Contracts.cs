namespace DataBro.Modules.Identity.Application;

// Auth DTOs (docs/API_SPEC.md §5 Auth, docs/SECURITY.md §1).

public sealed record RegisterRequest(string Email, string Password, string DisplayName);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record ConfirmEmailRequest(Guid UserId, string Token);

public sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresInSeconds);

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles);
