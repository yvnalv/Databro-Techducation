using System.Security.Claims;
using DataBro.Platform.Abstractions;
using Microsoft.AspNetCore.Http;

namespace DataBro.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Resolves the authenticated user from the current request's JWT claims. Replaces NullCurrentUser
/// so audit fields (CreatedBy/UpdatedBy) and the author-of-record are populated.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            var value = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? principal?.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}
