using DataBro.Modules.Content.Application;
using DataBro.Modules.Content.Domain;
using DataBro.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

internal sealed class LessonContentRepository(ContentDbContext db) : ILessonContentRepository
{
    public async Task AddAsync(LessonContent lesson, CancellationToken ct = default)
        => await db.LessonContents.AddAsync(lesson, ct);

    public Task<LessonContent?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.LessonContents
            // Versions loaded so publish can append against a tracked collection and restore can
            // read one, matching how the article path loads its aggregate.
            .Include("_versions")
            .FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<PagedResult<LessonContent>> ListAllAsync(
        PageRequest page, CancellationToken ct = default)
    {
        // No version history: a picker row shows a title and a status, and history is the heaviest
        // thing on the aggregate.
        var query = db.LessonContents.AsNoTracking().OrderByDescending(l => l.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);

        return new PagedResult<LessonContent>(items, page.Page, page.PageSize, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
