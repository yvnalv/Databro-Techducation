using System.Text.Json.Nodes;

namespace DataBro.Modules.Content.Domain;

/// <summary>
/// Collects the media ids a content document references.
///
/// Content stores ids, not URLs — a URL baked into a block would go stale the moment storage or the
/// CDN moves, and Content is not allowed to know how Media builds one anyway. So the ids are
/// gathered here and resolved in one batch through <c>IMediaDirectory</c> (ADR-0008), rather than
/// per block, which on an article with a dozen figures would be a dozen lookups on the cached
/// public read path.
/// </summary>
public static class ContentMedia
{
    /// <summary>Matches <see cref="ContentText"/>: guards against a malformed document nesting forever.</summary>
    private const int MaxDepth = 6;

    public static IReadOnlyCollection<Guid> Collect(ContentDocument? document, SeoMetadata? seo = null)
    {
        var ids = new HashSet<Guid>();

        if (document is not null)
            CollectBlocks(document.Blocks, ids, depth: 0);

        // The share image is a media reference like any other, and it is the one most likely to be
        // forgotten — it lives in SEO metadata rather than in the body.
        if (Guid.TryParse(seo?.OgImageMediaId, out var ogImage))
            ids.Add(ogImage);

        return ids;
    }

    private static void CollectBlocks(IReadOnlyList<ContentBlock> blocks, HashSet<Guid> ids, int depth)
    {
        if (depth > MaxDepth) return;

        foreach (var block in blocks)
        {
            if (block.Data is null) continue;

            if (block.Type == "image" &&
                block.Data["mediaId"] is JsonValue value &&
                value.TryGetValue<string>(out var mediaId) &&
                Guid.TryParse(mediaId, out var id))
            {
                ids.Add(id);
            }

            // List items may carry nested blocks (ADR-0009), and a figure inside a tutorial step is
            // a perfectly ordinary thing to write.
            if (block.Type == "list" && block.Data["items"] is JsonArray items)
            {
                foreach (var item in items.OfType<JsonObject>())
                {
                    if (item["blocks"] is JsonArray nested)
                        CollectBlocks(ToBlocks(nested), ids, depth + 1);
                }
            }
        }
    }

    private static IReadOnlyList<ContentBlock> ToBlocks(JsonArray nested)
    {
        var blocks = new List<ContentBlock>();

        foreach (var node in nested.OfType<JsonObject>())
        {
            var type = node["type"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(type)) continue;

            blocks.Add(new ContentBlock
            {
                Id = node["id"]?.GetValue<string>() ?? string.Empty,
                Type = type,
                Data = node["data"] as JsonObject,
            });
        }

        return blocks;
    }
}
