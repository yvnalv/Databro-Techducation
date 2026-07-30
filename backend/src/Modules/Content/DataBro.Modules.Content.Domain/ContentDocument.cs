using System.Text.Json.Nodes;

namespace DataBro.Modules.Content.Domain;

/// <summary>
/// A typed content block (docs/CONTENT_MODEL.md §2). Persisted inside the article's JSONB body.
/// <see cref="Data"/> is a free-form JSON object whose shape depends on <see cref="Type"/>.
/// </summary>
public sealed class ContentBlock
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public JsonObject? Data { get; init; }
}

/// <summary>
/// The full block-based body of a content unit: an ordered list of blocks plus a schema version.
/// Stored as JSONB (draft and published snapshots).
/// </summary>
public sealed class ContentDocument
{
    public int Version { get; init; } = 1;
    public IReadOnlyList<ContentBlock> Blocks { get; init; } = [];

    public static ContentDocument Empty => new();

    public bool HasContent => Blocks.Count > 0;

    /// <summary>Estimated reading time in minutes (~200 wpm) from text-bearing blocks.</summary>
    public int EstimateReadingTimeMinutes()
    {
        var words = 0;
        foreach (var block in Blocks)
        {
            var text = block.Data?["text"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(text))
                words += text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        return Math.Max(1, (int)Math.Ceiling(words / 200.0));
    }
}
