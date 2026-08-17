using System.Text.Json.Nodes;
using DataBro.Modules.Content.Domain;
using Xunit;

namespace DataBro.Modules.Content.Tests.Domain;

/// <summary>
/// A lesson body is the content engine and nothing else (ADR-0012). These tests pin that it gets the
/// *same* engine as an article — not a second implementation that can drift.
/// </summary>
public class LessonContentTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    private static ContentDocument Doc(string text) =>
        new()
        {
            Version = 1,
            Blocks = [new ContentBlock { Id = "b0", Type = "paragraph", Data = new JsonObject { ["text"] = text } }],
        };

    private static LessonContent NewDraft(ContentDocument? blocks = null, string title = "Chunking Strategies") =>
        LessonContent.CreateDraft(
            Guid.NewGuid(), Slug.Create("chunking-strategies"), title, "How to split documents",
            blocks ?? Doc("Fixed-size windows are the usual starting point."));

    [Fact]
    public void Starts_as_an_unpublished_draft_at_version_zero()
    {
        var lesson = NewDraft();

        Assert.Equal(ArticleStatus.Draft, lesson.Status);
        Assert.Equal(0, lesson.CurrentVersion);
        Assert.Null(lesson.PublishedBlocks);
        Assert.Empty(lesson.Versions);
    }

    [Fact]
    public void Publishing_snapshots_the_body_and_appends_a_version()
    {
        var lesson = NewDraft();

        var result = lesson.Publish(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ArticleStatus.Published, lesson.Status);
        Assert.Equal(1, lesson.CurrentVersion);
        Assert.NotNull(lesson.PublishedBlocks);
        Assert.Single(lesson.Versions);
    }

    [Fact]
    public void Publishing_snapshots_the_title_too_so_a_draft_edit_stays_private()
    {
        // The same CT-6 protection an article gets, because it lives in the engine rather than
        // being reimplemented per type.
        var lesson = NewDraft();
        lesson.Publish(Now);

        lesson.UpdateDraft("HALF-WRITTEN", "not ready", Doc("Draft body."));

        Assert.Equal("Chunking Strategies", lesson.PublishedTitle);
        Assert.Equal("HALF-WRITTEN", lesson.Title);
    }

    [Fact]
    public void Raises_its_own_publish_event_never_the_article_one()
    {
        // The point of the OnPublished hook. A subscriber reacting to ArticlePublished — cache
        // invalidation for a public article URL, say — must not be triggered by a lesson.
        var lesson = NewDraft();
        lesson.Publish(Now);

        Assert.Contains(lesson.DomainEvents, e => e is LessonContentPublishedDomainEvent);
        Assert.DoesNotContain(lesson.DomainEvents, e => e is ArticlePublishedDomainEvent);

        lesson.Unpublish();
        Assert.Contains(lesson.DomainEvents, e => e is LessonContentUnpublishedDomainEvent);
        Assert.DoesNotContain(lesson.DomainEvents, e => e is ArticleUnpublishedDomainEvent);
    }

    [Fact]
    public void Version_history_is_its_own_type()
    {
        // Each unit type keeps history in its own table, so the snapshot must be the matching
        // concrete type or it would be written to the wrong one.
        var lesson = NewDraft();
        lesson.Publish(Now);

        Assert.All(lesson.Versions, v => Assert.IsType<LessonContentVersion>(v));
    }

    [Fact]
    public void Gets_the_same_publish_preconditions_as_an_article()
    {
        var empty = LessonContent.CreateDraft(
            Guid.NewGuid(), Slug.Create("empty"), "Titled", "A summary", ContentDocument.Empty);

        Assert.Equal("business_rule_violation", empty.Publish(Now).Error.Code);

        var untitled = NewDraft(title: "   ");
        Assert.Equal("business_rule_violation", untitled.Publish(Now).Error.Code);
    }

    [Fact]
    public void Gets_scheduling_and_restore_without_reimplementing_them()
    {
        var lesson = NewDraft(Doc("First cut."));

        Assert.True(lesson.Schedule(Now.AddDays(1), Now).IsSuccess);
        Assert.Equal(ArticleStatus.Scheduled, lesson.Status);
        Assert.True(lesson.CancelSchedule().IsSuccess);
        Assert.Equal(ArticleStatus.Draft, lesson.Status);

        lesson.Publish(Now);
        lesson.UpdateDraft("Rewritten", "New summary", Doc("Second cut."));

        Assert.True(lesson.RestoreVersion(1).IsSuccess);
        Assert.Equal("Chunking Strategies", lesson.Title);
        // History untouched, published copy untouched (CT-8).
        Assert.Single(lesson.Versions);
        Assert.Equal("First cut.", lesson.PublishedBlocks!.Blocks[0].Data!["text"]!.GetValue<string>());
    }
}
