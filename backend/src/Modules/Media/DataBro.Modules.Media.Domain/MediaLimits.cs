namespace DataBro.Modules.Media.Domain;

/// <summary>
/// Upload limits (ADR-0011). These are a security boundary, not a product preference — the comments
/// say which attack each one closes, because a limit whose reason is forgotten is a limit somebody
/// eventually raises "to be helpful".
/// </summary>
public static class MediaLimits
{
    /// <summary>
    /// 10 MB. Comfortably fits a photograph off a phone; small enough that buffering and decoding
    /// one cannot become a denial of service.
    /// </summary>
    public const long MaxBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Maximum pixels in either dimension, checked against the image's <b>header</b> before any
    /// decode.
    ///
    /// This is the decompression-bomb defence, and the byte limit above does not substitute for it:
    /// a 100 MB PNG of a single flat colour is a few hundred KB compressed and roughly 14 GB once
    /// decoded to a pixel buffer. Reading dimensions from the header costs nothing and refuses it
    /// before a single pixel is allocated.
    /// </summary>
    public const int MaxDimension = 12_000;

    /// <summary>
    /// Total pixel budget. Catches the shape the per-dimension cap misses — 11,000 × 11,000 passes
    /// <see cref="MaxDimension"/> and is still 121 megapixels, about 480 MB decoded.
    /// </summary>
    public const long MaxPixels = 50_000_000;

    /// <summary>Alt text is a sentence, not an essay; it is also rendered into HTML attributes.</summary>
    public const int MaxAltTextLength = 500;

    public const int MaxFileNameLength = 255;

    /// <summary>
    /// Variant widths, narrow to wide. Chosen against the article column: the reading measure caps
    /// around 720 CSS px, so 640/960 cover it at 1× and 2×, and 1280/1920 serve full-bleed use and
    /// dense displays.
    ///
    /// A variant is only produced when the original is genuinely wider — upscaling invents detail
    /// and costs bytes to deliver a blurrier picture.
    /// </summary>
    public static readonly IReadOnlyList<int> VariantWidths = [640, 960, 1280, 1920];

    /// <summary>
    /// Re-encode quality for lossy output. 82 is the usual knee of the quality/size curve: visually
    /// indistinguishable from 95 on photographs at a fraction of the bytes.
    /// </summary>
    public const int JpegQuality = 82;
}
