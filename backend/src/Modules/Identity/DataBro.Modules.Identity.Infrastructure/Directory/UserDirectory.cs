using DataBro.Modules.Identity.Infrastructure.Persistence;
using DataBro.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Identity.Infrastructure.Directory;

/// <summary>
/// Identity's implementation of the shared <see cref="IUserDirectory"/> contract (ADR-0008).
/// This is the only sanctioned way for another module to learn a user's display name.
/// </summary>
internal sealed class UserDirectory(IdentityModuleDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<Guid, UserSummary>> GetUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<Guid, UserSummary>();

        var distinctIds = userIds.Distinct().ToArray();

        var users = await db.Users
            .AsNoTracking()
            .Where(u => distinctIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.AvatarMediaId })
            .ToListAsync(ct);

        // AvatarMediaId is a Media reference; resolving it to a URL is Media's job, so the avatar
        // stays null until that module lands rather than being guessed at here.
        return users.ToDictionary(
            u => u.Id,
            u => new UserSummary(u.Id, u.DisplayName, AvatarUrl: null));
    }
}
