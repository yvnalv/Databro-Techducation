using DataBro.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace DataBro.Modules.Content.Application;

/// <summary>
/// Publishes scheduled articles whose time has arrived (rule CT-7). Invoked on a recurring schedule
/// by the background runner (Hangfire); the method is idempotent-friendly — it only ever acts on
/// articles still marked <c>Scheduled</c> with a due time, so a re-run publishes nothing twice.
///
/// <para>
/// CT-7's failure contract: if an article can no longer satisfy the publish preconditions when its
/// time arrives, it is <em>not</em> silently dropped — it stays scheduled and an alert is raised, so
/// an editor can fix it. (The alert is a logged error until the Notification module exists.)
/// </para>
/// </summary>
public sealed class ScheduledPublishingJob(
    IArticleRepository repository,
    IClock clock,
    ILogger<ScheduledPublishingJob> logger)
{
    public async Task<int> PublishDueAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var due = await repository.ListDueScheduledAsync(now, ct);
        if (due.Count == 0) return 0;

        var published = 0;
        foreach (var article in due)
        {
            var result = article.Publish(now);
            if (result.IsFailure)
            {
                // Left scheduled deliberately: Publish makes no mutation on failure, so the row is
                // untouched and the next sweep will retry once the editor fixes it (CT-7).
                logger.LogError(
                    "Scheduled publish for article {ArticleId} ({Slug}) failed at {Now:o}: {Error}. It remains scheduled.",
                    article.Id, article.Slug.Value, now, result.Error.Message);
                continue;
            }

            published++;
            logger.LogInformation(
                "Scheduled article {ArticleId} ({Slug}) published automatically at {Now:o}.",
                article.Id, article.Slug.Value, now);
        }

        // One save for the whole sweep; only the successful publishes carry pending changes.
        await repository.SaveChangesAsync(ct);
        return published;
    }
}
