using System.Text;
using System.Text.Json.Nodes;

namespace DataBro.Modules.Content.Domain;

/// <summary>
/// Flattens a block document to plain text.
///
/// Two callers, one definition on purpose: reading-time estimation and the search projection
/// (ADR-0010) must agree on what counts as an article's words. Two extractors would drift, and the
/// drift would be invisible — a reading time that ignores half the body looks exactly like a short
/// article.
///
/// Blocks whose text is machine syntax rather than prose (embed URLs, LaTeX) are deliberately
/// excluded: they add tokens a reader never searches for and would dilute ranking.
/// </summary>
public static class ContentText
{
    /// <summary>
    /// Guards against a hand-crafted or corrupted document nesting list blocks without bound. Real
    /// authoring nests one, maybe two deep.
    /// </summary>
    private const int MaxDepth = 6;

    public static string Extract(ContentDocument? document)
    {
        if (document is null) return string.Empty;

        var builder = new StringBuilder();
        AppendBlocks(builder, document.Blocks, depth: 0);
        return builder.ToString().Trim();
    }

    /// <summary>Word count over the same text the search index sees.</summary>
    public static int CountWords(ContentDocument? document)
    {
        var text = Extract(document);
        return text.Length == 0
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static void AppendBlocks(StringBuilder builder, IReadOnlyList<ContentBlock> blocks, int depth)
    {
        if (depth > MaxDepth) return;

        foreach (var block in blocks)
            AppendBlock(builder, block, depth);
    }

    private static void AppendBlock(StringBuilder builder, ContentBlock block, int depth)
    {
        var data = block.Data;
        if (data is null) return;

        switch (block.Type)
        {
            // Plain-text headings by design (ADR-0009): emphasis inside a heading hurts both the
            // outline and anchor generation, so there is no inline content to walk.
            case "heading":
                AppendString(builder, data["text"]);
                break;

            // The `content` array is the ADR-0009 shape; `text` is the pre-ADR string. Renderers
            // accept both, so the projection must too, or search silently misses old documents.
            case "paragraph":
            case "callout":
                AppendInline(builder, data["content"]);
                AppendString(builder, data["text"]);
                break;

            case "quote":
                AppendInline(builder, data["content"]);
                AppendString(builder, data["text"]);
                AppendString(builder, data["attribution"]);
                break;

            // Code is indexed: "read_csv" is exactly the kind of thing a learner searches for, and
            // it is often the most specific token an article contains.
            case "code":
                AppendString(builder, data["code"]);
                AppendString(builder, data["filename"]);
                AppendString(builder, data["output"]);
                break;

            case "image":
                AppendString(builder, data["alt"]);
                AppendString(builder, data["caption"]);
                break;

            case "list":
                AppendListItems(builder, data["items"] as JsonArray, depth);
                break;

            case "table":
                AppendCells(builder, data["headers"] as JsonArray);
                foreach (var row in (data["rows"] as JsonArray)?.OfType<JsonNode>() ?? [])
                    AppendCells(builder, row as JsonArray);
                break;

            // `divider` has nothing; `embed` is a URL and `math` is LaTeX — syntax, not prose.
            default:
                break;
        }
    }

    private static void AppendListItems(StringBuilder builder, JsonArray? items, int depth)
    {
        foreach (var item in items?.OfType<JsonNode>() ?? [])
        {
            // A list item is either a bare string (legacy) or { content, blocks? } (ADR-0009).
            if (item is JsonValue)
            {
                AppendString(builder, item);
                continue;
            }

            if (item is not JsonObject entry) continue;

            AppendInline(builder, entry["content"]);

            if (entry["blocks"] is JsonArray nested)
                AppendBlocks(builder, ToBlocks(nested), depth + 1);
        }
    }

    private static void AppendCells(StringBuilder builder, JsonArray? cells)
    {
        foreach (var cell in cells?.OfType<JsonNode>() ?? [])
        {
            // A cell is inline content or a bare string; blocks never nest into cells.
            if (cell is JsonValue) AppendString(builder, cell);
            else AppendInline(builder, cell);
        }
    }

    /// <summary>
    /// Walks an inline node array (ADR-0009), collecting text nodes. Atomic nodes such as
    /// <c>mathInline</c> carry no text and are skipped.
    /// </summary>
    private static void AppendInline(StringBuilder builder, JsonNode? content)
    {
        if (content is not JsonArray nodes) return;

        foreach (var node in nodes.OfType<JsonObject>())
            AppendString(builder, node["text"]);
    }

    private static void AppendString(StringBuilder builder, JsonNode? node)
    {
        if (node is not JsonValue value) return;
        if (!value.TryGetValue<string>(out var text)) return;
        if (string.IsNullOrWhiteSpace(text)) return;

        if (builder.Length > 0) builder.Append(' ');
        builder.Append(text.Trim());
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
