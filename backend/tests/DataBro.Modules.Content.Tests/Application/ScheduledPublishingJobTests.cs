using System.Text.Json.Nodes;
using DataBro.Modules.Content.Application;
using DataBro.Modules.Content.Domain;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Tests.Application;

/// <summary>Rule CT-7: due scheduled articles publish automatically; a failure stays scheduled.</summary>
public class ScheduledPublishingJobTests
{
    private static readonly DateTimeOffset Base = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    private static ContentDocument Doc(bool withContent = true) => new()
    {
        Version = 1,
        Blocks = withContent
            ? [new ContentBlock { Id = "b0", Type = "paragraph", Data = new JsonObject { ["text"] = "Body." } }]
            : [],
    };

    private static Article ScheduledArticle(DateTimeOffset scheduledFor)
    {
        var article = Article.CreateDraft(
            Guid.NewGuid(), Slug.Create("scheduled-post"), "Scheduled Post", "s", Guid.NewGuid(), Doc());
        var result = article.Schedule(scheduledFor, Base);
        Assert.True(result.IsSuccess);
        return article;
    }

    [Fact]
    public async Task Publishes_articles_whose_time_has_arrived()
    {
        var article = ScheduledArticle(Base.AddMinutes(5));
        var repo = new FakeArticleRepository([article]);
        var job = new ScheduledPublishingJob(repo, new FixedClock(Base.AddMinutes(10)), NullLogger<ScheduledPublishingJob>.Instance);

        var count = await job.PublishDueAsync();

        Assert.Equal(1, count);
        Assert.Equal(ArticleStatus.Published, article.Status);
        Assert.Null(article.ScheduledFor);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task An_article_that_can_no_longer_publish_stays_scheduled()
    {
        // Scheduled while valid, then its draft was emptied — at publish time it fails CT-1 and, per
        // CT-7, must remain scheduled rather than being dropped.
        var article = ScheduledArticle(Base.AddMinutes(5));
        article.UpdateDraft("Scheduled Post", "s", Doc(withContent: false));

        var repo = new FakeArticleRepository([article]);
        var job = new ScheduledPublishingJob(repo, new FixedClock(Base.AddMinutes(10)), NullLogger<ScheduledPublishingJob>.Instance);

        var count = await job.PublishDueAsync();

        Assert.Equal(0, count);
        Assert.Equal(ArticleStatus.Scheduled, article.Status);
        Assert.Equal(Base.AddMinutes(5), article.ScheduledFor);
    }

    [Fact]
    public async Task Nothing_due_saves_nothing()
    {
        var repo = new FakeArticleRepository([]);
        var job = new ScheduledPublishingJob(repo, new FixedClock(Base), NullLogger<ScheduledPublishingJob>.Instance);

        Assert.Equal(0, await job.PublishDueAsync());
        Assert.Equal(0, repo.SaveCount);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    /// <summary>Only the two members the job touches are implemented; the rest are never called.</summary>
    private sealed class FakeArticleRepository(IReadOnlyList<Article> due) : IArticleRepository
    {
        public int SaveCount { get; private set; }

        public Task<IReadOnlyList<Article>> ListDueScheduledAsync(DateTimeOffset now, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Article>>(due.Where(a => a.ScheduledFor <= now).ToList());

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task AddAsync(Article article, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Article?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Article?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PagedResult<Article>> ListPublishedAsync(PageRequest page, Guid? categoryId = null, Guid? tagId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PagedResult<Article>> ListAllAsync(PageRequest page, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PagedResult<Article>> SearchPublishedAsync(string query, string locale, PageRequest page, bool fuzzy = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetTagIdsAsync(IReadOnlyCollection<Guid> articleIds, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
