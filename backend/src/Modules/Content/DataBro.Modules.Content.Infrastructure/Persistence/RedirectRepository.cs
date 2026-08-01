using DataBro.Modules.Content.Application;
using DataBro.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

internal sealed class RedirectRepository(ContentDbContext db) : IRedirectRepository
{
    public async Task AddAsync(Redirect redirect, CancellationToken ct = default)
        => await db.Redirects.AddAsync(redirect, ct);

    public Task<Redirect?> FindByFromPathAsync(string fromPath, CancellationToken ct = default)
    {
        var normalized = Redirect.NormalizePath(fromPath);
        return db.Redirects.FirstOrDefaultAsync(r => r.FromPath == normalized, ct);
    }

    public async Task<IReadOnlyList<Redirect>> ListPointingToAsync(string toPath, CancellationToken ct = default)
    {
        var normalized = Redirect.NormalizePath(toPath);
        return await db.Redirects.Where(r => r.ToPath == normalized).ToListAsync(ct);
    }

    public void Remove(Redirect redirect) => db.Redirects.Remove(redirect);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
