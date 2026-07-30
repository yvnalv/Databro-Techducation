using DataBro.Platform.Abstractions;

namespace DataBro.Platform.Persistence;

/// <summary>
/// Fallback <see cref="ICurrentUser"/> used until the Identity module supplies the authenticated
/// user from the JWT. Reports no user, so audit stamps are left null.
/// </summary>
public sealed class NullCurrentUser : ICurrentUser
{
    public Guid? UserId => null;
}
