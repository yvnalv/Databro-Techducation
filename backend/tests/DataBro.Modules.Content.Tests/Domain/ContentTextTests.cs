using System.Text.Json.Nodes;
using DataBro.Modules.Content.Domain;
using Xunit;

namespace DataBro.Modules.Content.Tests.Domain;

/// <summary>
/// The plain-text projection feeding the search index (ADR-0010) and reading-time estimation.
/// What it silently drops is unfindable, so these tests pin the shape of every block type.
/// </summary>
public class ContentTextTests
{
    private static ContentDocument Doc(params ContentBlock[] blocks) =>
        new() { Version = 1, Blocks = blocks };

    private static ContentBlock Block(string type, JsonObject data) =>
        new() { Id = Guid.NewGuid().ToString("N"), Type = type, Data = data };

    /// <summary>An ADR-0009 inline node array.</summary>
    private static JsonArray Inline(params string[] texts) =>
        new(texts.Select(t => (JsonNode)new JsonObject { ["type"] = "text", ["text"] = t }).ToArray());

    [Fact]
    public void Extracts_rich_text_paragraph_content()
    {
        var text = ContentText.Extract(Doc(
            Block("paragraph", new JsonObject { ["content"] = Inline("Retrieval", "augmented generation") })));

        Assert.Equal("Retrieval augmented generation", text);
    }

    [Fact]
    public void Extracts_legacy_plain_text_paragraphs()
    {
        // Documents written before ADR-0009 carry a plain string. Renderers accept both shapes, so
        // the projection must too — otherwise old articles quietly stop being searchable.
        var text = ContentText.Extract(Doc(Block("paragraph", new JsonObject { ["text"] = "Older shape." })));

        Assert.Equal("Older shape.", text);
    }

    [Fact]
    public void Extracts_headings_code_images_and_quotes()
    {
        var text = ContentText.Extract(Doc(
            Block("heading", new JsonObject { ["level"] = 2, ["text"] = "Chunking" }),
            Block("code", new JsonObject
            {
                ["language"] = "python",
                ["code"] = "df.read_csv('x')",
                ["filename"] = "load.py",
                ["output"] = "ok",
            }),
            Block("image", new JsonObject { ["mediaId"] = "m1", ["alt"] = "A diagram", ["caption"] = "Figure 1" }),
            Block("quote", new JsonObject { ["content"] = Inline("Measure twice"), ["attribution"] = "Anon" })));

        Assert.Equal("Chunking df.read_csv('x') load.py ok A diagram Figure 1 Measure twice Anon", text);
    }

    [Fact]
    public void Extracts_list_items_including_nested_blocks()
    {
        var text = ContentText.Extract(Doc(
            Block("list", new JsonObject
            {
                ["ordered"] = true,
                ["items"] = new JsonArray(
                    "A legacy string item",
                    new JsonObject
                    {
                        ["content"] = Inline("Install the client"),
                        ["blocks"] = new JsonArray(new JsonObject
                        {
                            ["id"] = "n1",
                            ["type"] = "code",
                            ["data"] = new JsonObject { ["code"] = "pip install databro" },
                        }),
                    }),
            })));

        Assert.Equal("A legacy string item Install the client pip install databro", text);
    }

    [Fact]
    public void Extracts_table_headers_and_cells()
    {
        var text = ContentText.Extract(Doc(
            Block("table", new JsonObject
            {
                ["headers"] = new JsonArray("Model", (JsonNode)Inline("Latency")),
                ["rows"] = new JsonArray(new JsonArray("Haiku", "fast")),
            })));

        Assert.Equal("Model Latency Haiku fast", text);
    }

    [Fact]
    public void Skips_machine_syntax_blocks()
    {
        // An embed URL and a LaTeX expression are tokens nobody searches for; indexing them only
        // dilutes ranking.
        var text = ContentText.Extract(Doc(
            Block("embed", new JsonObject { ["provider"] = "youtube", ["url"] = "https://youtu.be/abc" }),
            Block("math", new JsonObject { ["latex"] = @"\frac{a}{b}" }),
            Block("divider", [])));

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void Skips_atomic_inline_nodes_that_carry_no_text()
    {
        var text = ContentText.Extract(Doc(
            Block("paragraph", new JsonObject
            {
                ["content"] = new JsonArray(
                    new JsonObject { ["type"] = "text", ["text"] = "where" },
                    new JsonObject { ["type"] = "mathInline", ["attrs"] = new JsonObject { ["latex"] = "x^2" } },
                    new JsonObject { ["type"] = "text", ["text"] = "holds" }),
            })));

        Assert.Equal("where holds", text);
    }

    [Fact]
    public void Reading_time_counts_rich_text_not_only_the_legacy_shape()
    {
        // Regression: the previous estimator read `data.text` directly, so every ADR-0009 paragraph
        // counted as zero words and a long article reported the one-minute floor.
        var words = Enumerable.Range(0, 600).Select(i => $"word{i}").ToArray();
        var document = Doc(Block("paragraph", new JsonObject { ["content"] = Inline(words) }));

        Assert.Equal(600, ContentText.CountWords(document));
        Assert.Equal(3, document.EstimateReadingTimeMinutes());
    }

    [Fact]
    public void Empty_document_projects_to_empty_text_and_the_one_minute_floor()
    {
        Assert.Equal(string.Empty, ContentText.Extract(ContentDocument.Empty));
        Assert.Equal(0, ContentText.CountWords(ContentDocument.Empty));
        Assert.Equal(1, ContentDocument.Empty.EstimateReadingTimeMinutes());
    }
}
