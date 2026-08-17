using DataBro.Modules.Content.Application;
using DataBro.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

/// <summary>
/// Slug uniqueness across every content unit table (ADR-0012).
///
/// Two indexed existence checks rather than one, because the tables are separate by design. Both are
/// covered by each table's unique slug index, so this is two index probes on a write path — not
/// something worth optimising away.
/// </summary>
internal sealed class ContentSlugRegistry(ContentDbContext db) : IContentSlugRegistry
{
    public async Task<bool> IsTakenAsync(string slug, Guid? excluding = null, CancellationToken ct = default)
    {
        if (!SlugParser.TryParse(slug, out var parsed))
            return false;

        // The soft-delete query filter applies to both, so a deleted unit does not hold its slug
        // hostage — which matches how the per-table unique indexes are filtered.
        if (await db.Articles.AnyAsync(a => a.Slug == parsed && a.Id != excluding, ct))
            return true;

        return await db.LessonContents.AnyAsync(l => l.Slug == parsed && l.Id != excluding, ct);
    }
}
