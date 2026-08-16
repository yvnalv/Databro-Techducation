namespace DataBro.Platform.Abstractions;

/// <summary>
/// One rendered size of an image asset. <paramref name="Width"/> is what a browser needs to pick
/// from a <c>srcset</c>, so it is part of the contract rather than a detail of the URL.
/// </summary>
public sealed record MediaVariantSummary(string Name, string Url, int Width, int Height);

/// <summary>
/// An image asset as other modules are allowed to see it: enough to render a responsive
/// <c>&lt;img&gt;</c> and nothing more. Ownership, checksums and storage keys stay inside Media.
/// </summary>
/// <param name="Variants">
/// Empty while the asset is still being processed (ADR-0011). A consumer renders the original at
/// full size and simply omits <c>srcset</c> — never a broken image.
/// </param>
public sealed record MediaSummary(
    Guid Id,
    string Url,
    string AltText,
    int Width,
    int Height,
    IReadOnlyList<MediaVariantSummary> Variants);

/// <summary>
/// Read-only cross-module lookup of media assets, owned by Media and consumed by any module that
/// stores a media id — Content today (image blocks and <c>og:image</c>), Learning later.
/// <para>
/// Lives in <c>Platform</c> for the same reason as <see cref="IUserDirectory"/>: modules must not
/// depend on one another, so the shared kernel holds the interface and Media supplies the
/// implementation through DI. See ADR-0008.
/// </para>
/// <para>
/// Batch-shaped, and here that matters more than it does for authors: one article can carry a dozen
/// image blocks, so a per-item lookup would be an N+1 on the cached public read path.
/// </para>
/// </summary>
public interface IMediaDirectory
{
    /// <summary>
    /// Resolves the given media ids. Ids with no matching asset are absent from the result, so
    /// callers must tolerate a partial map — an image deleted out from under an article must leave
    /// the article renderable.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, MediaSummary>> GetMediaAsync(
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken ct = default);
}
