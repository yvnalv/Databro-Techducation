using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataBro.Modules.Identity.Domain;
using Xunit;

namespace DataBro.Modules.Content.Tests.Api;

/// <summary>
/// Full-text search (ADR-0010). These run against a real PostgreSQL container on purpose: the index
/// is a generated <c>tsvector</c> column and the ranking is <c>ts_rank</c>, so an in-memory fake
/// would verify nothing that matters.
/// </summary>
public class SearchApiTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    private Task<HttpClient> EditorClientAsync() => factory.CreateAuthenticatedClientAsync(Roles.Editor);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    /// <summary>
    /// The articles segment of the response.
    ///
    /// Search is segmented per module since ADR-0014, so these tests read Content's own segment
    /// rather than the whole payload. What they assert — ranking, stemming, the typo fallback, draft
    /// exclusion — is unchanged; only where it is read from moved.
    /// </summary>
    private static JsonElement Articles(JsonElement root) =>
        root.GetProperty("data").GetProperty("segments").EnumerateArray()
            .Single(s => s.GetProperty("kind").GetString() == "articles");

    private static string[] Slugs(JsonElement root) =>
        Articles(root).GetProperty("hits").EnumerateArray()
            .Select(item => item.GetProperty("slug").GetString()!)
            .ToArray();

    /// <summary>Creates and publishes an article, returning its slug.</summary>
    private static async Task<string> PublishAsync(
        HttpClient editor, string title, string summary, string body, string locale = "en")
    {
        var slug = $"search-{Guid.NewGuid():N}";

        var create = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/articles", new
        {
            title,
            summary,
            slug,
            locale,
            content = new
            {
                version = 1,
                blocks = new[]
                {
                    new
                    {
                        id = "b0",
                        type = "paragraph",
                        data = new { content = new[] { new { type = "text", text = body } } },
                    },
                },
            },
        }));

        var id = create.GetProperty("data").GetProperty("id").GetGuid();
        var publish = await editor.PostAsync($"/api/v1/authoring/articles/{id}/publish", null);
        publish.EnsureSuccessStatusCode();

        return slug;
    }

    [Fact]
    public async Task Finds_a_published_article_by_a_word_in_its_body()
    {
        var editor = await EditorClientAsync();
        var token = $"zeppelin{Guid.NewGuid():N}"[..20];
        var slug = await PublishAsync(editor, "A Body Match", "Nothing notable here", $"The {token} appears only in the body.");

        var root = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/search?q={token}"));

        Assert.Contains(slug, Slugs(root));
        Assert.Equal("exact", Articles(root).GetProperty("matchMode").GetString());
    }

    [Fact]
    public async Task Drafts_are_not_searchable()
    {
        var editor = await EditorClientAsync();
        var token = $"unpublishedtoken{Guid.NewGuid():N}"[..24];

        var create = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/articles", new
        {
            title = token,
            summary = "A draft",
            slug = $"draft-{Guid.NewGuid():N}",
            content = new { version = 1, blocks = new[] { new { id = "b0", type = "paragraph", data = new { text = "Body." } } } },
        }));
        Assert.Equal("draft", create.GetProperty("data").GetProperty("status").GetString());

        var root = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/search?q={token}"));

        Assert.Equal(0, Articles(root).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Title_matches_outrank_body_matches()
    {
        var editor = await EditorClientAsync();
        var token = $"hydrofoil{Guid.NewGuid():N}"[..20];

        // Published body-first so recency ordering would put it on top if ranking were ignored.
        var body = await PublishAsync(editor, "Something Else Entirely", "A summary", $"A passing mention of {token} here.");
        var title = await PublishAsync(editor, $"All About {token}", "A summary", "Body without the term.");

        var root = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/search?q={token}"));
        var slugs = Slugs(root);

        Assert.Equal(title, slugs[0]);
        Assert.Contains(body, slugs);
    }

    [Fact]
    public async Task English_stemming_matches_a_different_word_form()
    {
        var editor = await EditorClientAsync();

        // Real words, not a random token with a suffix bolted on: the Snowball stemmer works on
        // English morphology, so "gyroscopeab12f9s" is not a plural of anything.
        var slug = await PublishAsync(editor, "Stemmed", "A summary", "We calibrate gyroscopes for every rig.");

        // Indexed as the plural, queried as the singular — only stemming connects the two.
        var root = await ReadAsync(await factory.CreateClient().GetAsync("/api/v1/search?q=gyroscope"));

        Assert.Contains(slug, Slugs(root));
    }

    [Fact]
    public async Task A_typo_falls_back_to_trigram_similarity_and_says_so()
    {
        var editor = await EditorClientAsync();
        var slug = await PublishAsync(editor, "Kubernetes Autoscaling Explained", "A summary", "Body text.");

        // The typo has to survive stemming to be a typo at all: "Kubernets" stems to "kubernet",
        // exactly like the correct spelling, so full-text would match it and never reach the
        // fallback. "Kubernettes" stems to "kubernett" and genuinely misses.
        var root = await ReadAsync(await factory.CreateClient().GetAsync("/api/v1/search?q=Kubernettes%20Autoscaling"));

        Assert.Equal("fuzzy", Articles(root).GetProperty("matchMode").GetString());
        Assert.Contains(slug, Slugs(root));
    }

    [Fact]
    public async Task A_single_misspelt_word_matches_a_much_longer_title()
    {
        var editor = await EditorClientAsync();
        var slug = await PublishAsync(
            editor, "Retrieval-Augmented Generation, End to End", "A summary", "Body text.");

        // Regression: whole-string `similarity()` divides by the title's length, scoring this pair
        // 0.14 — below any usable threshold — so a one-word typo against a long title matched
        // nothing at all. `word_similarity()` scores the best matching run of words instead (0.43).
        var root = await ReadAsync(await factory.CreateClient().GetAsync("/api/v1/search?q=Retreival"));

        Assert.Equal("fuzzy", Articles(root).GetProperty("matchMode").GetString());
        Assert.Contains(slug, Slugs(root));
    }

    [Fact]
    public async Task Search_is_scoped_to_one_locale()
    {
        var editor = await EditorClientAsync();
        var token = $"kelinci{Guid.NewGuid():N}"[..18];
        var indonesian = await PublishAsync(editor, $"Panduan {token}", "Ringkasan", "Isi artikel.", locale: "id");

        var english = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/search?q={token}"));
        Assert.DoesNotContain(indonesian, Slugs(english));

        var scoped = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/search?q={token}&locale=id"));
        Assert.Contains(indonesian, Slugs(scoped));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public async Task Queries_shorter_than_two_characters_return_nothing(string query)
    {
        var root = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/search?q={query}"));

        Assert.Equal(0, Articles(root).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Operator_soup_does_not_error()
    {
        // `to_tsquery` raises a syntax error on this; `websearch_to_tsquery` does not. A public
        // search box must never 500 because someone typed a stray ampersand.
        var response = await factory.CreateClient().GetAsync("/api/v1/search?q=%26%20%7C%20!%20%3A*%20%22unclosed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
