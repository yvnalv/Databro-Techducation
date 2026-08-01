using DataBro.Modules.Content.Application;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DataBro.Modules.Content.Infrastructure;

/// <summary>
/// Registers the Content module's recurring background jobs once the host is up. Owned by the module
/// (docs/ARCHITECTURE.md — each module wires its own work); the host only stands up the Hangfire
/// server and storage.
///
/// <para>
/// The recurring sweep publishes scheduled articles as their time arrives (rule CT-7). It is only
/// registered where a Hangfire server will actually run it — a server-less host (integration tests)
/// leaves it unregistered and never touches Hangfire storage.
/// </para>
/// </summary>
internal sealed class ContentJobsInitializer(IServiceProvider services, IConfiguration configuration)
    : IHostedService
{
    public const string ScheduledPublishJobId = "content:scheduled-publish";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Hangfire:EnableServer", true))
            return Task.CompletedTask;

        // Resolved lazily (not injected) so a server-less host never forces Hangfire storage to init.
        var recurring = services.GetRequiredService<IRecurringJobManager>();
        recurring.AddOrUpdate<ScheduledPublishingJob>(
            ScheduledPublishJobId,
            job => job.PublishDueAsync(CancellationToken.None),
            Cron.Minutely());

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
