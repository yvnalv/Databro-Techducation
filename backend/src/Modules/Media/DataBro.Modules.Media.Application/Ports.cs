using DataBro.Modules.Media.Domain;

namespace DataBro.Modules.Media.Application;

/// <summary>
/// Object storage for media bytes (ADR-0011). Implemented once over S3 — MinIO in development,
/// DigitalOcean Spaces in production, which differ only by endpoint and credentials.
/// </summary>
public interface IMediaStorage
{
    /// <summary>
    /// Stores <paramref name="content"/> at <paramref name="key"/>, overwriting any existing object.
    /// Overwrite rather than fail: keys carry the asset id, so the only way to collide is a retry of
    /// the same upload, and that should converge rather than error.
    /// </summary>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// The publicly reachable URL for a stored key. Pure string composition against configuration —
    /// no signing, because media is public content served through a CDN.
    /// </summary>
    string UrlFor(string key);
}

/// <summary>The dimensions of an image, read from its header without decoding it.</summary>
public sealed record ImageDimensions(int Width, int Height);

/// <summary>A processed image: its bytes, format and resulting dimensions.</summary>
public sealed record ProcessedImage(byte[] Bytes, ImageFormat Format, int Width, int Height);

/// <summary>
/// Image decoding, re-encoding and resizing (ADR-0011). Behind a port so the domain and application
/// never reference an imaging library — the licence on that library is the single most likely thing
/// in this module to be swapped.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Reads dimensions from the image header <b>without decoding the pixels</b>. This is what makes
    /// the decompression-bomb check cheap enough to run before allocating anything (see
    /// <see cref="MediaLimits.MaxDimension"/>); returns null when the bytes are not a readable image.
    /// </summary>
    ImageDimensions? ReadDimensions(Stream content);

    /// <summary>
    /// Decodes and re-encodes at full size, stripping all metadata. The output is the canonical copy
    /// — the uploaded bytes are never stored (ADR-0011).
    /// </summary>
    ProcessedImage Normalize(Stream content, ImageFormat output);

    /// <summary>
    /// Resizes to <paramref name="width"/>, preserving aspect ratio. Never upscales — the caller is
    /// responsible for skipping widths larger than the source.
    /// </summary>
    ProcessedImage Resize(Stream content, int width, ImageFormat output);
}

/// <summary>Persistence port for the <see cref="MediaAsset"/> aggregate.</summary>
public interface IMediaAssetRepository
{
    Task AddAsync(MediaAsset asset, CancellationToken ct = default);

    Task<MediaAsset?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Resolves many assets at once for the cross-module directory. Variants are included: the whole
    /// point of the lookup is rendering a <c>srcset</c>.
    /// </summary>
    Task<IReadOnlyList<MediaAsset>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Newest first — the picker shows what was just uploaded at the top.</summary>
    Task<(IReadOnlyList<MediaAsset> Items, int Total)> ListAsync(
        int skip, int take, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
