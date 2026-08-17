using System.Text.Json.Nodes;
using DataBro.Modules.Content.Domain;
using Xunit;
using DataBro.Platform.SharedKernel;

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
    public void Schedule_sets_status_and_time_for_a_valid_future_request()
    {
        var article = NewDraft();

        var result = article.Schedule(Now.AddHours(1), Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ArticleStatus.Scheduled, article.Status);
        Assert.Equal(Now.AddHours(1), article.ScheduledFor);
    }

    [Fact]
    public void Schedule_rejects_a_time_in_the_past()
    {
        var article = NewDraft();

        var result = article.Schedule(Now.AddMinutes(-1), Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ArticleStatus.Draft, article.Status);
        Assert.Null(article.ScheduledFor);
    }

    [Fact]
    public void Schedule_requires_content_and_a_title()
    {
        Assert.True(NewDraft(Doc()).Schedule(Now.AddHours(1), Now).IsFailure);   // no blocks
        Assert.True(NewDraft(title: "  ").Schedule(Now.AddHours(1), Now).IsFailure); // no title
    }

    [Fact]
    public void A_published_article_cannot_be_scheduled()
    {
        var article = NewDraft();
        article.Publish(Now);

        var result = article.Schedule(Now.AddHours(1), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
    }

    [Fact]
    public void ChangeSlug_returns_the_previous_slug()
    {
        var article = NewDraft();

        var previous = article.ChangeSlug(Slug.Create("deep-learning"));

        Assert.NotNull(previous);
        Assert.Equal("intro-to-ml", previous!.Value);
        Assert.Equal("deep-learning", article.Slug.Value);
    }

    [Fact]
    public void ChangeSlug_to_the_same_value_is_a_no_op()
    {
        var article = NewDraft();

        var previous = article.ChangeSlug(Slug.Create("intro-to-ml"));

        Assert.Null(previous);
        Assert.Equal("intro-to-ml", article.Slug.Value);
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

    // ---- Cancelling a schedule (CT-7) ----

    [Fact]
    public void Cancelling_a_schedule_returns_the_article_to_draft()
    {
        var article = NewDraft();
        article.Schedule(Now.AddDays(7), Now);

        var result = article.CancelSchedule();

        Assert.True(result.IsSuccess);
        Assert.Equal(ArticleStatus.Draft, article.Status);
        Assert.Null(article.ScheduledFor);
    }

    [Fact]
    public void Cancelling_a_schedule_leaves_the_draft_untouched()
    {
        // Cancelling is a decision about *when*, not about *what*.
        var article = NewDraft(Doc("The body as written."));
        article.Schedule(Now.AddDays(7), Now);

        article.CancelSchedule();

        Assert.True(article.DraftBlocks.HasContent);
        Assert.Equal("Intro to ML", article.Title);
    }

    [Fact]
    public void Only_a_scheduled_article_can_have_its_schedule_cancelled()
    {
        var article = NewDraft();

        Assert.Equal("conflict", article.CancelSchedule().Error.Code);

        article.Publish(Now);
        Assert.Equal("conflict", article.CancelSchedule().Error.Code);
    }

    // ---- Version history (CT-8) ----

    [Fact]
    public void Restoring_a_version_copies_it_into_the_draft()
    {
        var article = NewDraft(Doc("First cut."));
        article.Publish(Now);

        article.UpdateDraft("Rewritten", "A new summary", Doc("Second cut."));
        article.Publish(Now.AddDays(1));

        var result = article.RestoreVersion(1);

        Assert.True(result.IsSuccess);
        Assert.Equal("Intro to ML", article.Title);
        Assert.Equal("First cut.", article.DraftBlocks.Blocks[0].Data!["text"]!.GetValue<string>());
    }

    [Fact]
    public void Restoring_never_mutates_history_or_what_a_reader_sees()
    {
        // The heart of CT-8. Restoring loads old content into the *draft*; the published copy and
        // every version row stay exactly as they were, so a restore is itself recorded when it is
        // published rather than rewriting the past.
        var article = NewDraft(Doc("First cut."));
        article.Publish(Now);
        article.UpdateDraft("Rewritten", "A new summary", Doc("Second cut."));
        article.Publish(Now.AddDays(1));

        article.RestoreVersion(1);

        Assert.Equal(2, article.Versions.Count);
        Assert.Equal(2, article.CurrentVersion);
        Assert.Equal(ArticleStatus.Published, article.Status);
        Assert.Equal("Second cut.", article.PublishedBlocks!.Blocks[0].Data!["text"]!.GetValue<string>());
    }

    [Fact]
    public void Publishing_a_restored_version_appends_rather_than_rewrites()
    {
        var article = NewDraft(Doc("First cut."));
        article.Publish(Now);
        article.UpdateDraft("Rewritten", "A new summary", Doc("Second cut."));
        article.Publish(Now.AddDays(1));

        article.RestoreVersion(1);
        article.Publish(Now.AddDays(2));

        Assert.Equal(3, article.CurrentVersion);
        Assert.Equal(3, article.Versions.Count);
        // Version 3 carries the restored content, and versions 1 and 2 are still their own selves.
        Assert.Equal("First cut.", article.Versions.Single(v => v.Version == 3).Blocks.Blocks[0].Data!["text"]!.GetValue<string>());
        Assert.Equal("Second cut.", article.Versions.Single(v => v.Version == 2).Blocks.Blocks[0].Data!["text"]!.GetValue<string>());
    }

    [Fact]
    public void Restoring_recomputes_reading_time_from_the_restored_content()
    {
        // CT-12: reading time is derived, never carried over from whatever the draft happened to say.
        var article = NewDraft(Doc(string.Join(" ", Enumerable.Repeat("word", 800))));
        article.Publish(Now);
        var longReadingTime = article.ReadingTimeMinutes;

        article.UpdateDraft("Short", "A summary", Doc("Tiny."));
        Assert.Equal(1, article.ReadingTimeMinutes);

        article.RestoreVersion(1);

        Assert.Equal(longReadingTime, article.ReadingTimeMinutes);
        Assert.True(article.ReadingTimeMinutes > 1);
    }

    [Fact]
    public void Restoring_a_version_that_does_not_exist_is_a_not_found()
    {
        var article = NewDraft();
        article.Publish(Now);

        var result = article.RestoreVersion(99);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
