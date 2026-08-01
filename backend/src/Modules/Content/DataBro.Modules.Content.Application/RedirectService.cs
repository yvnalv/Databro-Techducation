using DataBro.Modules.Content.Domain;

namespace DataBro.Modules.Content.Application;

/// <summary>
/// The public path a content unit lives at on the <c>site</c> app. Centralized so the redirect a
/// slug change writes always matches the URL the frontend actually serves.
/// </summary>
public static class ContentPaths
{
    public static string Article(string slug) => $"/articles/{slug}";
    public static string Category(string slug) => $"/categories/{slug}";
    public static string Tag(string slug) => $"/tags/{slug}";
}

/// <summary>
/// Owns the <c>redirects</c> table (docs/SEO.md §4): the public lookup the <c>site</c> app hits on a
/// 404, and the write-side helper the slug-change use cases call to record a 301.
///
/// <para>
/// <see cref="RecordAsync"/> deliberately does not save: it enlists the redirect in the same unit of
/// work as the slug change, so the old URL and its redirect commit together or not at all (CT-3).
/// </para>
/// </summary>
public sealed class RedirectService(IRedirectRepository redirects)
{
    /// <summary>
    /// Resolves a path to its redirect target, or null when none exists. One hop is enough because
    /// <see cref="RecordAsync"/> collapses chains on write, so a stored redirect always points at a
    /// live page rather than another redirect.
    /// </summary>
    public async Task<RedirectDto?> ResolveAsync(string path, CancellationToken ct = default)
    {
        var redirect = await redirects.FindByFromPathAsync(path, ct);
        return redirect is null ? null : new RedirectDto(redirect.FromPath, redirect.ToPath, redirect.StatusCode);
    }

    /// <summary>
    /// Records a move from <paramref name="fromPath"/> to <paramref name="toPath"/>, keeping the
    /// redirect graph collapsed: existing redirects that pointed at the old path are repointed at the
    /// new one, and any redirect leaving the new path is dropped because that path is now live again.
    /// The caller's <c>SaveChangesAsync</c> commits this with the slug change.
    /// </summary>
    public async Task RecordAsync(string fromPath, string toPath, string reason, CancellationToken ct = default)
    {
        var from = Redirect.NormalizePath(fromPath);
        var to = Redirect.NormalizePath(toPath);
        if (from == to) return;

        // The destination is a live page now: it must not itself redirect anywhere.
        if (await redirects.FindByFromPathAsync(to, ct) is { } leavingDestination)
            redirects.Remove(leavingDestination);

        // Collapse chains: a → from becomes a → to, so a crawler never follows two hops.
        foreach (var inbound in await redirects.ListPointingToAsync(from, ct))
        {
            if (inbound.FromPath == to)
                redirects.Remove(inbound); // repointing this would make it redirect to itself
            else
                inbound.RepointTo(to);
        }

        // Record old → new, reusing a row already keyed on the old path rather than colliding with it.
        if (await redirects.FindByFromPathAsync(from, ct) is { } existing)
            existing.RepointTo(to);
        else
            await redirects.AddAsync(Redirect.Create(Guid.NewGuid(), from, to, reason), ct);
    }
}
