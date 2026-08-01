using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Domain;

/// <summary>
/// A stored URL redirect (docs/DATABASE.md — <c>redirects</c>; docs/SEO.md §4). Written when a
/// published article's or a taxonomy term's slug changes, so the old path never 404s (rules CT-3,
/// TX-1). Consumed by the <c>site</c> app, which serves the <see cref="StatusCode"/> (301 by default).
///
/// <para>
/// A redirect is a lightweight record, not an aggregate root: it has no invariants of its own beyond
/// its shape and is only ever created or repointed by the aggregate whose slug moved.
/// </para>
/// </summary>
public sealed class Redirect : Entity
{
    /// <summary>The old, now-dead path (normalized). Unique — one destination per source path.</summary>
    public string FromPath { get; private set; } = string.Empty;

    /// <summary>The live path the source now points at (normalized).</summary>
    public string ToPath { get; private set; } = string.Empty;

    /// <summary>HTTP status the site serves. 301 (permanent) by default; 302 is reserved for future use.</summary>
    public int StatusCode { get; private set; }

    /// <summary>Why the redirect exists (e.g. "article slug changed"), for auditing.</summary>
    public string? Reason { get; private set; }

    private Redirect() { } // EF

    public static Redirect Create(Guid id, string fromPath, string toPath, string? reason = null, int statusCode = 301)
    {
        var from = NormalizePath(fromPath);
        var to = NormalizePath(toPath);

        if (from == to)
            throw new ArgumentException("A redirect's source and destination cannot be the same path.");

        return new Redirect
        {
            Id = id,
            FromPath = from,
            ToPath = to,
            StatusCode = statusCode,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        };
    }

    /// <summary>
    /// Repoints an existing redirect at a new destination. Used to collapse chains: when
    /// <c>a → b</c> exists and <c>b</c> then moves to <c>c</c>, <c>a</c> is repointed straight to
    /// <c>c</c> so a crawler never has to follow two hops.
    /// </summary>
    public void RepointTo(string toPath) => ToPath = NormalizePath(toPath);

    /// <summary>
    /// Normalizes a path the way the edge (Nginx/CDN) does before comparison (docs/SEO.md §4):
    /// lowercased, exactly one leading slash, no trailing slash. Query and fragment are dropped —
    /// redirects are keyed on the path only.
    /// </summary>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A redirect path cannot be empty.", nameof(path));

        var trimmed = path.Trim();

        // Drop query/fragment so "/x?y" and "/x" collapse to one key.
        var cut = trimmed.IndexOfAny(['?', '#']);
        if (cut >= 0) trimmed = trimmed[..cut];

        var lowered = trimmed.ToLowerInvariant().Trim('/');
        return lowered.Length == 0 ? "/" : $"/{lowered}";
    }
}
