using DataBro.Modules.Content.Domain;

namespace DataBro.Modules.Content.Application;

/// <summary>
/// Persistence port for the <see cref="Redirect"/> record. Shares the Content unit of work, so a
/// redirect written alongside a slug change commits with it (CT-3 is atomic).
/// </summary>
public interface IRedirectRepository
{
    Task AddAsync(Redirect redirect, CancellationToken ct = default);

    /// <summary>The redirect whose source is <paramref name="fromPath"/> (normalized), or null.</summary>
    Task<Redirect?> FindByFromPathAsync(string fromPath, CancellationToken ct = default);

    /// <summary>
    /// Every redirect currently pointing <em>at</em> <paramref name="toPath"/> (normalized). Used to
    /// collapse chains when that destination itself moves.
    /// </summary>
    Task<IReadOnlyList<Redirect>> ListPointingToAsync(string toPath, CancellationToken ct = default);

    void Remove(Redirect redirect);

    Task SaveChangesAsync(CancellationToken ct = default);
}
