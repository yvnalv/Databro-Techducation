using DataBro.Modules.Media.Application;
using DataBro.Platform.Abstractions;

namespace DataBro.Modules.Media.Infrastructure.Directory;

/// <summary>
/// Media's implementation of the shared <see cref="IMediaDirectory"/> contract (ADR-0008, ADR-0011).
/// This is the only sanctioned way for another module to turn a media id into a URL.
/// </summary>
internal sealed class MediaDirectory(IMediaAssetRepository repository, IMediaStorage storage) : IMediaDirectory
{
    public async Task<IReadOnlyDictionary<Guid, MediaSummary>> GetMediaAsync(
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken ct = default)
    {
        if (mediaIds.Count == 0)
            return new Dictionary<Guid, MediaSummary>();

        var assets = await repository.GetByIdsAsync(mediaIds, ct);

        return assets.ToDictionary(
            a => a.Id,
            a => new MediaSummary(
                a.Id,
                storage.UrlFor(a.StorageKey),
                a.AltText,
                a.Width,
                a.Height,
                // Ordered narrow to wide: a `srcset` is a list of candidates, and emitting them in
                // width order is what makes it readable in view-source.
                a.Variants
                    .OrderBy(v => v.Width)
                    .Select(v => new MediaVariantSummary(v.Name, storage.UrlFor(v.StorageKey), v.Width, v.Height))
                    .ToList()));
    }
}
