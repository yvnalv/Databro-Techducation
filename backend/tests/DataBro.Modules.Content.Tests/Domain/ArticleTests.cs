using System.Text.Json.Nodes;
using DataBro.Modules.Content.Domain;
using Xunit;

namespace DataBro.Modules.Content.Tests.Domain;

public class ArticleTests
{
    private static readonly Guid Author = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    private static ContentDocument Doc(params string[] paragraphs) =>
        new()
        {
            Version = 1,
            Blocks = paragraphs
                .Select((t, i) => new ContentBlock
                {
                    Id = $"b{i}",
                    Type = "paragraph",
                    Data = new JsonObject { ["text"] = t },
                })
                .ToList(),
        };

    private static Article NewDraft(ContentDocument? blocks = null, string title = "Intro to ML") =>
        Article.CreateDraft(Guid.NewGuid(), Slug.Create("intro-to-ml"), title, "A summary",
            Author, blocks ?? Doc("Hello world."));

    [Fact]
    public void CreateDraft_starts_unpublished_at_version_zero()
    {
        var article = NewDraft();

        Assert.Equal(ArticleStatus.Draft, article.Status);
        Assert.Equal(0, article.CurrentVersion);
        Assert.Null(article.PublishedBlocks);
        Assert.True(article.ReadingTimeMinutes >= 1);
        Assert.Empty(article.Versions);
    }

    [Fact]
    public void Publish_requires_at_least_one_block()
    {
        var article = NewDraft(Doc()); // no blocks

        var result = article.Publish(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("business_rule_violation", result.Error.Code);
        Assert.Equal(ArticleStatus.Draft, article.Status);
    }

    [Fact]
    public void Publish_requires_a_title()
    {
        var article = NewDraft(title: "   ");

        var result = article.Publish(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("business_rule_violation", result.Error.Code);
    }

    [Fact]
    public void Publish_snapshots_blocks_and_raises_event()
    {
        var article = NewDraft();

        var result = article.Publish(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ArticleStatus.Published, article.Status);
        Assert.Equal(1, article.CurrentVersion);
        Assert.NotNull(article.PublishedBlocks);
        Assert.Equal(Now, article.PublishedAt);
        Assert.Single(article.Versions);
        Assert.Contains(article.DomainEvents, e => e is ArticlePublishedDomainEvent);
    }

    [Fact]
    public void Republishing_appends_an_immutable_version()
    {
        var article = NewDraft();
        article.Publish(Now);

        article.UpdateDraft("Intro to ML v2", "Updated", Doc("First.", "Second."));
        var second = article.Publish(Now.AddDays(1));

        Assert.True(second.IsSuccess);
        Assert.Equal(2, article.CurrentVersion);
        Assert.Equal(2, article.Versions.Count);
        Assert.Equal(new[] { 1, 2 }, article.Versions.Select(v => v.Version).OrderBy(v => v));
    }

    [Fact]
    public void Unpublish_only_valid_when_published()
    {
        var article = NewDraft();

        var draftAttempt = article.Unpublish();
        Assert.True(draftAttempt.IsFailure);
        Assert.Equal("conflict", draftAttempt.Error.Code);

        article.Publish(Now);
        var published = article.Unpublish();
        Assert.True(published.IsSuccess);
        Assert.Equal(ArticleStatus.Unpublished, article.Status);
        Assert.Contains(article.DomainEvents, e => e is ArticleUnpublishedDomainEvent);
    }
}
