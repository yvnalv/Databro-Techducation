using DataBro.Modules.Learning.Infrastructure.Persistence;
using DataBro.Platform.Persistence.Outbox;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DataBro.Modules.Learning.Infrastructure.Messaging;

/// <summary>Drains Learning's outbox on a schedule.</summary>
public sealed class LearningOutboxJob(
    OutboxProcessor<LearningDbContext> processor,
    ILogger<LearningOutboxJob> logger)
{
    /// <summary>
    /// One sweep. Several batches per run so a backlog clears in one pass rather than one batch a
    /// minute, but bounded so a runaway queue cannot occupy the worker indefinitely.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task DrainAsync(CancellationToken ct = default)
    {
        var total = 0;

        for (var pass = 0; pass < 10; pass++)
        {
            var handled = await processor.ProcessBatchAsync(ct: ct);
            total += handled;

            if (handled == 0) break;
        }

        if (total > 0)
            logger.LogInformation("Outbox: dispatched {Count} Learning message(s).", total);
    }
}

/// <summary>
/// Registers the sweep, mirroring how Content registers its scheduled-publish job: the module owns
/// its own work and the host only stands up the Hangfire server.
/// </summary>
internal sealed class LearningJobsInitializer(IServiceProvider services, IConfiguration configuration)
    : IHostedService
{
    public const string OutboxJobId = "learning:outbox";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Hangfire:EnableServer", true))
            return Task.CompletedTask;

        var recurring = services.GetRequiredService<IRecurringJobManager>();

        // Minutely, matching the scheduled-publish sweep. A completion email arriving up to a minute
        // late is unremarkable; the guarantee being bought here is that it arrives at all.
        recurring.AddOrUpdate<LearningOutboxJob>(
            OutboxJobId,
            job => job.DrainAsync(CancellationToken.None),
            Cron.Minutely());

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
