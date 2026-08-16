namespace DataBro.Modules.Media.Domain;

/// <summary>An image format the platform accepts or produces (ADR-0011).</summary>
public enum ImageFormat
{
    Jpeg,
    Png,
    Webp,
    Gif,
}

/// <summary>
/// Where an asset is in its processing lifecycle (ADR-0011). Variants are produced by a background
/// job, so an asset is usable at full size before it is responsive.
/// </summary>
public enum MediaProcessingStatus
{
    /// <summary>Original stored; variants not generated yet. Renders without a <c>srcset</c>.</summary>
    Pending,

    Ready,

    /// <summary>
    /// Variant generation failed after its retries. The original is still stored and serveable — a
    /// failed resize must not cost an author their upload.
    /// </summary>
    Failed,
}

/// <summary>
/// Format detection and the accept/produce policy (ADR-0011).
///
/// Detection is by <b>magic bytes</b>, never by the request's <c>Content-Type</c> or the filename's
/// extension: both are supplied by the caller, and a caller who is uploading something hostile is
/// exactly the caller who will lie about them.
/// </summary>
public static class MediaFormats
{
    /// <summary>
    /// Longest signature this needs to see. Callers only have to buffer this many bytes to identify
    /// a file.
    /// </summary>
    public const int SignatureLength = 12;

    public static string MimeTypeOf(ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => "image/jpeg",
        ImageFormat.Png => "image/png",
        ImageFormat.Webp => "image/webp",
        ImageFormat.Gif => "image/gif",
        _ => "application/octet-stream",
    };

    public static string ExtensionOf(ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => "jpg",
        ImageFormat.Png => "png",
        ImageFormat.Webp => "webp",
        ImageFormat.Gif => "gif",
        _ => "bin",
    };

    /// <summary>
    /// Identifies an image by its leading bytes, or null when it is not a format we accept.
    ///
    /// SVG is deliberately absent and must stay absent: it is XML, it can execute script, and unlike
    /// a raster format it cannot be neutralised by re-encoding.
    /// </summary>
    public static ImageFormat? Detect(ReadOnlySpan<byte> header)
    {
        // JPEG: FF D8 FF
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ImageFormat.Jpeg;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return ImageFormat.Png;

        // GIF: "GIF87a" / "GIF89a"
        if (header.Length >= 6 &&
            header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F')
            return ImageFormat.Gif;

        // WebP: "RIFF" .... "WEBP" — the four size bytes in between are skipped, so both parts of
        // the signature must be checked. "RIFF" alone is also AVI and WAV.
        if (header.Length >= 12 &&
            header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
            header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
            return ImageFormat.Webp;

        return null;
    }

    /// <summary>
    /// The format an accepted upload is re-encoded to.
    ///
    /// PNG stays PNG because it is the format that carries screenshots and diagrams, where JPEG's
    /// ringing around text is obvious. Animated GIFs stay GIF — re-encoding one to a still frame
    /// would silently destroy the thing the author uploaded. Everything else becomes JPEG.
    /// </summary>
    public static ImageFormat OutputFor(ImageFormat source) => source switch
    {
        ImageFormat.Png => ImageFormat.Png,
        ImageFormat.Gif => ImageFormat.Gif,
        _ => ImageFormat.Jpeg,
    };
}
