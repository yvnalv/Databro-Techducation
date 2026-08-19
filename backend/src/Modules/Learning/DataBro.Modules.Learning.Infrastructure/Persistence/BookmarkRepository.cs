using DataBro.Modules.Learning.Application;
using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Learning.Infrastructure.Persistence;

internal sealed class BookmarkRepository(LearningDbContext db) : IBookmarkRepository
{
    public async Task AddAsync(Bookmark bookmark, CancellationToken ct = default)
        => await db.Bookmarks.AddAsync(bookmark, ct);

    public async Task<PagedResult<Bookmark>> ListForUserAsync(
        Guid userId, PageRequest page, CancellationToken ct = default)
    {
        var query = db.Bookmarks
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.SavedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);

        return new PagedResult<Bookmark>(items, page.Page, page.PageSize, total);
    }

    public Task<Bookmark?> FindAsync(Guid userId, BookmarkKind kind, Guid targetId, CancellationToken ct = default)
        => db.Bookmarks.FirstOrDefaultAsync(
            b => b.UserId == userId && b.Kind == kind && b.TargetId == targetId, ct);

    // Soft-deleted by the auditing interceptor (XC-1), so the row survives and the filtered unique
    // index is what lets the same thing be saved again afterwards.
    public void Remove(Bookmark bookmark) => db.Bookmarks.Remove(bookmark);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
