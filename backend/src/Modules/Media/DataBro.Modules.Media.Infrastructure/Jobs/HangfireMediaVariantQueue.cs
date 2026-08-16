using DataBro.Modules.Media.Application;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace DataBro.Modules.Media.Infrastructure.Jobs;

/// <summary>
/// Hands variant generation to Hangfire (ADR-0011). The Application layer depends on
/// <see cref="IMediaVariantQueue"/>, not on Hangfire, for the same reason Content's scheduled
/// publishing does: the scheduler is an infrastructure choice.
/// </summary>
internal sealed class HangfireMediaVariantQueue(IBackgroundJobClient jobs) : IMediaVariantQueue
{
    public void Enqueue(Guid assetId)
        => jobs.Enqueue<MediaVariantJob>(job => job.RunAsync(assetId, CancellationToken.None));
}

/// <summary>
/// The job Hangfire invokes. A thin shell over <see cref="MediaService.GenerateVariantsAsync"/> so
/// the work itself stays testable without a scheduler.
/// </summary>
public sealed class MediaVariantJob(MediaService media)
{
    // Automatic retries: the failure modes here are transient (object storage hiccup, a cold
    // connection), and GenerateVariantsAsync is idempotent, so re-running is always safe.
    [AutomaticRetry(Attempts = 3)]
    public Task RunAsync(Guid assetId, CancellationToken ct) => media.GenerateVariantsAsync(assetId, ct);
}

/// <summary>
/// Runs variant generation inline when no Hangfire server is present — integration tests, and any
/// host started with <c>Hangfire:EnableServer=false</c>.
///
/// Without this the tests would enqueue into storage nothing ever drains, and every assertion about
/// variants would hang or silently pass against a Pending asset.
/// </summary>
internal sealed class InlineMediaVariantQueue(IServiceScopeFactory scopes) : IMediaVariantQueue
{
    public void Enqueue(Guid assetId)
    {
        // Its own scope: the request's DbContext is mid-save when this is called, and sharing it
        // would reuse a context whose change tracker is already committed to something else.
        using var scope = scopes.CreateScope();
        var media = scope.ServiceProvider.GetRequiredService<MediaService>();
        media.GenerateVariantsAsync(assetId, CancellationToken.None).GetAwaiter().GetResult();
    }
}
