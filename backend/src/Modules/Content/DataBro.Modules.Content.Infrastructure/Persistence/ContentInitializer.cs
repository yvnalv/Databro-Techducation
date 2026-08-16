using DataBro.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

/// <summary>
/// In Development, applies pending Content migrations so a fresh clone self-provisions its database
/// (never in production — see docs/DEPLOYMENT.md). The search backfill below runs everywhere.
/// </summary>
public sealed class ContentInitializer(
    IServiceProvider services,
    IHostEnvironment environment,
    ILogger<ContentInitializer> logger) : IHostedService
{
    /// <summary>Rows per save. Small enough to keep the transaction short on a large catalogue.</summary>
    private const int BatchSize = 200;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        if (environment.IsDevelopment())
            await db.Database.MigrateAsync(cancellationToken);

        await BackfillSearchTextAsync(db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Fills <c>search_text</c> for articles published before the column existed (ADR-0010).
    ///
    /// The generated vector picks up title and summary for every row the moment the migration runs,
    /// but the body projection is computed in C# from typed blocks, so SQL cannot derive it — an
    /// article published last week would be findable by its title and by nothing in its body until
    /// someone happened to republish it.
    ///
    /// Idempotent and self-limiting: it only ever selects published rows whose projection is still
    /// empty, so on every run after the first it does one indexless-but-cheap count and stops.
    /// </summary>
    private async Task BackfillSearchTextAsync(ContentDbContext db, CancellationToken ct)
    {
        // Ids first, then fixed chunks. A "select the next N empty rows" loop would never terminate
        // on an article whose body genuinely projects to nothing — an image-only post — because it
        // would be selected again on every pass.
        var pending = await db.Articles
            .Where(a => a.Status == ArticleStatus.Published && a.SearchText == string.Empty)
            .Select(a => a.Id)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        foreach (var chunk in pending.Chunk(BatchSize))
        {
            var batch = await db.Articles.Where(a => chunk.Contains(a.Id)).ToListAsync(ct);

            foreach (var article in batch)
                article.RebuildSearchText();

            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("Backfilled search text for {Count} published articles.", pending.Count);
    }
}
