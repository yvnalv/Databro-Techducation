using Microsoft.AspNetCore.Identity;

namespace DataBro.Modules.Identity.Infrastructure.Persistence;

/// <summary>Application user built on ASP.NET Core Identity. Doubles as the author profile source.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public Guid? AvatarMediaId { get; set; }
}

/// <summary>Application role (RBAC).</summary>
public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }
    public ApplicationRole(string name) : base(name) { }
}

/// <summary>
/// A hashed, rotatable refresh token (docs/SECURITY.md §1). Only the SHA-256 hash is stored; reuse
/// of a revoked token invalidates the chain.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}
