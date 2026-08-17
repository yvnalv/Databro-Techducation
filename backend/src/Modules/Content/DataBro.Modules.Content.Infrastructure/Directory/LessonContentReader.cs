using DataBro.Modules.Content.Domain;
using DataBro.Modules.Content.Infrastructure.Persistence;
using DataBro.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Content.Infrastructure.Directory;

/// <summary>
/// Content's implementation of <see cref="ILessonContentReader"/> (ADR-0008, ADR-0012). The only
/// sanctioned way for Learning to read a lesson body.
/// </summary>
internal sealed class LessonContentReader(ContentDbContext db) : ILessonContentReader
{
    public async Task<IReadOnlyDictionary<Guid, LessonContentView>> GetLessonContentAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, LessonContentView>();

        var distinct = ids.Distinct().ToArray();

        // No version history included: a consumer wants the current published body, and the history
        // rows are the heaviest thing on the aggregate. AsNoTracking because nothing here is edited.
        var bodies = await db.LessonContents
            .AsNoTracking()
            .Where(l => distinct.Contains(l.Id))
            .ToListAsync(ct);

        return bodies.ToDictionary(l => l.Id, ToView);
    }

    private static LessonContentView ToView(LessonContent lesson) =>
        new(
            lesson.Id,
            lesson.Slug.Value,
            // The published title, not the draft — the same CT-6 rule the public article surfaces
            // follow. Falls back only for a body that has never been published, where the draft
            // title is all there is and `PublishedAt` already says it is not live.
            lesson.PublishedTitle ?? lesson.Title,
            lesson.PublishedSummary ?? lesson.Summary,
            lesson.ReadingTimeMinutes,
            lesson.CurrentVersion,
            lesson.PublishedAt,
            // Published blocks only. An unpublished body yields an empty list rather than its draft.
            lesson.PublishedBlocks?.Blocks
                .Select(b => new ContentBlockView(b.Id, b.Type, b.Data))
                .ToList()
                ?? []);
}
