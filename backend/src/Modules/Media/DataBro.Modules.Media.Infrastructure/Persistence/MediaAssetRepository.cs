using DataBro.Modules.Media.Application;
using DataBro.Modules.Media.Domain;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Media.Infrastructure.Persistence;

internal sealed class MediaAssetRepository(MediaDbContext db) : IMediaAssetRepository
{
    public async Task AddAsync(MediaAsset asset, CancellationToken ct = default)
        => await db.Assets.AddAsync(asset, ct);

    public Task<MediaAsset?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Assets.Include(a => a.Variants).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<MediaAsset>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];

        var distinct = ids.Distinct().ToArray();

        return await db.Assets
            .AsNoTracking()
            .Include(a => a.Variants)
            .Where(a => distinct.Contains(a.Id))
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<MediaAsset> Items, int Total)> ListAsync(
        int skip, int take, CancellationToken ct = default)
    {
        var query = db.Assets.AsNoTracking().Include(a => a.Variants).OrderByDescending(a => a.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip(skip).Take(take).ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
