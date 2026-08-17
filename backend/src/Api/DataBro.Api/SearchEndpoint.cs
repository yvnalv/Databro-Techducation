using DataBro.Platform.Abstractions;
using DataBro.Platform.Web;

namespace DataBro.Api;

/// <summary>
/// Cross-module search (ADR-0014).
///
/// <para>
/// Mapped by the <b>host</b>, not by a module, and that is the whole point: composing results from
/// Content and Learning is something only the composition root may do. Putting it in either module
/// would mean that module knowing about the other.
/// </para>
/// <para>
/// The host does not know which modules search — it asks every registered
/// <see cref="IModuleSearch"/>. A third searchable module appears here by registering itself and
/// nothing in this file changes.
/// </para>
/// </summary>
public static class SearchEndpoint
{
    /// <summary>
    /// A single character matches most of a corpus under trigram similarity and nothing useful under
    /// full text. Answering "nothing" is more truthful than answering "everything".
    /// </summary>
    private const int MinQueryLength = 2;

    /// <summary>
    /// Per segment, not overall. Segments are shown side by side rather than paged, so this is a
    /// display cap; each segment reports its own true total alongside.
    /// </summary>
    private const int DefaultLimit = 10;
    private const int MaxLimit = 50;

    public static IEndpointRouteBuilder MapSearch(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/search", async (
            string? q,
            string? locale,
            int? limit,
            IEnumerable<IModuleSearch> modules,
            CancellationToken ct) =>
        {
            var query = q?.Trim() ?? string.Empty;
            var scope = string.Equals(locale, "id", StringComparison.OrdinalIgnoreCase) ? "id" : "en";
            var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

            var ordered = modules.OrderBy(m => m.Order).ToArray();

            if (query.Length < MinQueryLength)
            {
                // Empty segments rather than an empty object: the client renders the same shape
                // whether or not anything matched, and never has to special-case a missing key.
                return ApiEnvelope.Ok(new
                {
                    query,
                    segments = ordered.Select(m => Empty(m.Kind)).ToArray(),
                });
            }

            // Sequential rather than parallel: each module has its own scoped DbContext, and a
            // DbContext is not thread-safe. Two indexed lookups are not worth the risk of a
            // concurrency bug that only shows under load.
            var segments = new List<object>(ordered.Length);

            foreach (var module in ordered)
            {
                var segment = await module.SearchAsync(query, scope, take, ct);

                segments.Add(new
                {
                    kind = segment.Kind,
                    total = segment.Total,
                    matchMode = segment.MatchMode,
                    hits = segment.Hits,
                });
            }

            return ApiEnvelope.Ok(new { query, segments });
        }).WithTags("Search");

        return endpoints;
    }

    private static object Empty(string kind) =>
        new { kind, total = 0, matchMode = "exact", hits = Array.Empty<SearchHit>() };
}
