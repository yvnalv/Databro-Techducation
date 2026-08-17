using System.Net.Http.Json;
using System.Text.Json;
using DataBro.Modules.Content.Domain;
using DataBro.Modules.Content.Infrastructure.Persistence;
using DataBro.Modules.Identity.Domain;
using DataBro.Platform.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataBro.Modules.Learning.Tests;

/// <summary>
/// Cross-module search (ADR-0014). The point of these is the <em>segmentation</em>: two modules each
/// searching what they own, and nothing pretending their relevance scores are comparable.
/// </summary>
public class SearchApiTests(LearningApiFactory factory) : IClassFixture<LearningApiFactory>
{
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static JsonElement Segment(JsonElement root, string kind)
        => root.GetProperty("data").GetProperty("segments").EnumerateArray()
            .Single(s => s.GetProperty("kind").GetString() == kind);

    private static string[] Titles(JsonElement segment)
        => segment.GetProperty("hits").EnumerateArray()
            .Select(h => h.GetProperty("title").GetString()!)
            .ToArray();

    /// <summary>A published course whose title contains the token.</summary>
    private async Task PublishCourseAsync(string title)
    {
        var editor = await factory.CreateAuthenticatedClientAsync(Roles.Editor);
        var slug = $"course-{Guid.NewGuid():N}";

        var created = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/courses", new
        {
            title,
            summary = "A course summary",
            slug,
        }));
        var courseId = created.GetProperty("data").GetProperty("id").GetGuid();

        var withModule = await ReadAsync(await editor.PostAsJsonAsync(
            $"/api/v1/authoring/courses/{courseId}/modules", new { title = "Module" }));
        var moduleId = withModule.GetProperty("data").GetProperty("modules")[0].GetProperty("id").GetGuid();

        // A course needs at least one lesson to publish, so seed a body for it.
        Guid bodyId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            var body = LessonContent.CreateDraft(
                Guid.NewGuid(), Slug.Create($"body-{Guid.NewGuid():N}"), "A lesson", "A summary",
                new ContentDocument
                {
                    Version = 1,
                    Blocks =
                    [
                        new ContentBlock
                        {
                            Id = "b0",
                            Type = "paragraph",
                            Data = new System.Text.Json.Nodes.JsonObject { ["text"] = "Body." },
                        },
                    ],
                });
            body.Publish(DateTimeOffset.UtcNow);
            db.LessonContents.Add(body);
            await db.SaveChangesAsync();
            bodyId = body.Id;
        }

        await editor.PostAsJsonAsync(
            $"/api/v1/authoring/courses/{courseId}/modules/{moduleId}/lessons",
            new { contentUnitId = bodyId });

        (await editor.PostAsync($"/api/v1/authoring/courses/{courseId}/publish", null))
            .EnsureSuccessStatusCode();
    }

    private async Task PublishArticleAsync(string title)
    {
        var editor = await factory.CreateAuthenticatedClientAsync(Roles.Editor);

        var created = await ReadAsync(await editor.PostAsJsonAsync("/api/v1/authoring/articles", new
        {
            title,
            summary = "An article summary",
            slug = $"article-{Guid.NewGuid():N}",
            content = new
            {
                version = 1,
                blocks = new[] { new { id = "b0", type = "paragraph", data = new { text = "Body." } } },
            },
        }));

        await editor.PostAsync(
            $"/api/v1/authoring/articles/{created.GetProperty("data").GetProperty("id").GetGuid()}/publish",
            null);
    }

    [Fact]
    public async Task A_course_and_an_article_come_back_in_separate_segments()
    {
        // The defect this whole ADR exists to fix: before segmentation, searching for a course
        // returned articles and nothing else.
        var token = $"quasar{Guid.NewGuid():N}"[..16];
        await PublishCourseAsync($"{token} for Engineers");
        await PublishArticleAsync($"An article about {token}");

        var root = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/search?q={token}"));

        Assert.Single(Titles(Segment(root, "courses")));
        Assert.Single(Titles(Segment(root, "articles")));
    }

    [Fact]
    public async Task Courses_are_listed_before_articles()
    {
        // Order is part of the contract: a course is the larger commitment and the rarer answer.
        var root = await ReadAsync(await factory.CreateClient().GetAsync("/api/v1/search?q=anything"));

        var kinds = root.GetProperty("data").GetProperty("segments").EnumerateArray()
            .Select(s => s.GetProperty("kind").GetString())
            .ToArray();

        Assert.Equal(["courses", "articles"], kinds);
    }

    [Fact]
    public async Task An_unpublished_course_is_not_searchable()
    {
        var editor = await factory.CreateAuthenticatedClientAsync(Roles.Editor);
        var token = $"hidden{Guid.NewGuid():N}"[..16];

        await editor.PostAsJsonAsync("/api/v1/authoring/courses", new
        {
            title = $"{token} Draft Course",
            summary = "Never published",
            slug = $"draft-{Guid.NewGuid():N}",
        });

        var root = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/search?q={token}"));

        Assert.Equal(0, Segment(root, "courses").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Course_titles_are_stemmed_like_article_titles()
    {
        await PublishCourseAsync("Designing Embeddings");

        // Indexed as the plural, queried as the singular — only stemming connects the two.
        var root = await ReadAsync(await factory.CreateClient().GetAsync("/api/v1/search?q=embedding"));

        Assert.Contains("Designing Embeddings", Titles(Segment(root, "courses")));
    }

    [Fact]
    public async Task A_typo_falls_back_for_courses_too()
    {
        // Inconsistent typo tolerance between segments reads as a bug: a learner who mistypes should
        // not get corrected articles beside an empty course list.
        await PublishCourseAsync("Kubernetes Autoscaling");

        var root = await ReadAsync(
            await factory.CreateClient().GetAsync("/api/v1/search?q=Kubernettes%20Autoscaling"));
        var courses = Segment(root, "courses");

        Assert.Equal("fuzzy", courses.GetProperty("matchMode").GetString());
        Assert.Contains("Kubernetes Autoscaling", Titles(courses));
    }

    [Fact]
    public async Task Each_segment_reports_its_own_match_mode()
    {
        // Two modules can legitimately disagree, and flattening that to one flag would misreport
        // whichever segment lost.
        var root = await ReadAsync(await factory.CreateClient().GetAsync("/api/v1/search?q=zzzznothing"));

        foreach (var kind in new[] { "courses", "articles" })
        {
            var segment = Segment(root, kind);
            Assert.Equal(0, segment.GetProperty("total").GetInt32());
            Assert.Equal("exact", segment.GetProperty("matchMode").GetString());
        }
    }

    [Fact]
    public async Task A_hit_carries_the_path_its_owning_module_decided()
    {
        // The composing layer never has to know one kind's routing from another's.
        var token = $"pathcheck{Guid.NewGuid():N}"[..18];
        await PublishCourseAsync($"{token} Course");

        var hit = Segment(await ReadAsync(
            await factory.CreateClient().GetAsync($"/api/v1/search?q={token}")), "courses")
            .GetProperty("hits")[0];

        Assert.StartsWith("/courses/", hit.GetProperty("path").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public async Task A_query_under_two_characters_returns_empty_segments_not_a_missing_key(string query)
    {
        var root = await ReadAsync(await factory.CreateClient().GetAsync($"/api/v1/search?q={query}"));

        // Same shape either way, so the client never special-cases a missing segment.
        Assert.Equal(2, root.GetProperty("data").GetProperty("segments").GetArrayLength());
        Assert.Equal(0, Segment(root, "courses").GetProperty("total").GetInt32());
        Assert.Equal(0, Segment(root, "articles").GetProperty("total").GetInt32());
    }
}
