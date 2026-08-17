using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DataBro.Modules.Learning.Infrastructure.Persistence;

/// <summary>
/// In Development, applies pending Learning migrations so a fresh clone self-provisions its database
/// (never in production — see docs/DEPLOYMENT.md).
/// </summary>
public sealed class LearningInitializer(IServiceProvider services, IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
