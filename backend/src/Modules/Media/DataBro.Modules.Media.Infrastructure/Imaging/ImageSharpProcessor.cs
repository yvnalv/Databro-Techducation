using DataBro.Modules.Media.Application;
using DataBro.Modules.Media.Domain;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using DomainImageFormat = DataBro.Modules.Media.Domain.ImageFormat;

namespace DataBro.Modules.Media.Infrastructure.Imaging;

/// <summary>
/// Image processing on ImageSharp (ADR-0011).
///
/// Fully managed, so the container ships no native imaging dependency. Confined to Infrastructure
/// behind <see cref="IImageProcessor"/> because its licence — free for open source and under $1M
/// revenue — is the most likely thing in this module to force a swap.
/// </summary>
internal sealed class ImageSharpProcessor : IImageProcessor
{
    public ImageDimensions? ReadDimensions(Stream content)
    {
        try
        {
            // Header only: `Identify` parses metadata without allocating a pixel buffer, which is
            // what makes the decompression-bomb check cheap enough to run first (ADR-0011).
            var info = Image.Identify(content);
            return new ImageDimensions(info.Width, info.Height);
        }
        catch (Exception)
        {
            // Anything unreadable is "not an image" as far as the caller is concerned; it turns into
            // a 400, never a 500.
            return null;
        }
    }

    public ProcessedImage Normalize(Stream content, DomainImageFormat output)
    {
        using var image = Image.Load(content);

        StripMetadata(image);

        return Encode(image, output);
    }

    public ProcessedImage Resize(Stream content, int width, DomainImageFormat output)
    {
        using var image = Image.Load(content);

        StripMetadata(image);

        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(width, 0), // 0 height preserves the aspect ratio.
            Mode = ResizeMode.Max,
            // Lanczos3 is the quality default and the right trade for downscaling photographs and
            // screenshots, which is all this ever does.
            Sampler = KnownResamplers.Lanczos3,
        }));

        return Encode(image, output);
    }

    /// <summary>
    /// Drops EXIF, IPTC and XMP.
    ///
    /// Privacy, not size: a photograph off a phone routinely carries GPS coordinates, and an author
    /// dragging one into the editor is not consenting to publish their location. Re-encoding removes
    /// it anyway for most paths — this makes it explicit rather than incidental.
    ///
    /// The ICC profile goes too: without a colour-managed pipeline it is dead weight, and stripping
    /// it after decoding does not change the pixels.
    /// </summary>
    private static void StripMetadata(Image image)
    {
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IccProfile = null;
    }

    private static ProcessedImage Encode(Image image, DomainImageFormat output)
    {
        using var buffer = new MemoryStream();

        switch (output)
        {
            case DomainImageFormat.Png:
                image.SaveAsPng(buffer, new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression });
                break;

            case DomainImageFormat.Webp:
                image.SaveAsWebp(buffer, new WebpEncoder { Quality = MediaLimits.JpegQuality });
                break;

            case DomainImageFormat.Gif:
                // Animated GIFs keep every frame: re-encoding one to a still image would silently
                // destroy the thing the author uploaded.
                image.SaveAsGif(buffer, new GifEncoder());
                break;

            default:
                image.SaveAsJpeg(buffer, new JpegEncoder { Quality = MediaLimits.JpegQuality });
                break;
        }

        return new ProcessedImage(buffer.ToArray(), output, image.Width, image.Height);
    }
}
