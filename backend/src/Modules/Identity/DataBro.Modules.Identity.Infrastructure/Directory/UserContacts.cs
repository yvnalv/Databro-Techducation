using DataBro.Modules.Identity.Infrastructure.Persistence;
using DataBro.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Identity.Infrastructure.Directory;

/// <summary>
/// Identity's implementation of <see cref="IUserContacts"/> (ADR-0008).
///
/// <para>
/// Reads only what the contract promises. A projection rather than a whole entity, so a change to the
/// user aggregate cannot quietly widen what leaves this module.
/// </para>
/// </summary>
internal sealed class UserContacts(IdentityModuleDbContext db) : IUserContacts
{
    public async Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct = default)
    {
        var row = await db.Users
            .Where(u => u.Id == userId && u.Email != null)
            .Select(u => new { u.Id, u.Email, u.DisplayName })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : new UserContact(row.Id, row.Email!, row.DisplayName);
    }
}
