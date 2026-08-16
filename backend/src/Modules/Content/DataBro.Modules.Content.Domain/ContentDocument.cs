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

    /// <summary>
    /// Estimated reading time in minutes (~200 wpm) from text-bearing blocks.
    ///
    /// Counts through <see cref="ContentText"/> rather than reading <c>data.text</c> directly. The
    /// direct read predated ADR-0009 and saw only the legacy string shape, so every rich-text
    /// paragraph counted as zero words and long articles reported "1 min read".
    /// </summary>
    public int EstimateReadingTimeMinutes()
        => Math.Max(1, (int)Math.Ceiling(ContentText.CountWords(this) / 200.0));

    /// <summary>Plain-text projection of this document, for the search index (ADR-0010).</summary>
    public string ToPlainText() => ContentText.Extract(this);
}
