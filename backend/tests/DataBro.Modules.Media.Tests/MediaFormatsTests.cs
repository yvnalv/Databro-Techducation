using DataBro.Modules.Media.Domain;

namespace DataBro.Modules.Media.Tests;

/// <summary>
/// Format sniffing (ADR-0011). This is the gate every upload passes through first, so what it
/// accepts is a security boundary rather than a convenience.
/// </summary>
public class MediaFormatsTests
{
    [Fact]
    public void Detects_jpeg_png_and_gif_by_signature()
    {
        Assert.Equal(ImageFormat.Jpeg, MediaFormats.Detect(Fixtures.Jpeg(32, 32)));
        Assert.Equal(ImageFormat.Png, MediaFormats.Detect(Fixtures.Png(32, 32)));
        Assert.Equal(ImageFormat.Gif, MediaFormats.Detect("GIF89a...."u8));
    }

    [Fact]
    public void Requires_both_halves_of_the_webp_signature()
    {
        // "RIFF" alone is also AVI and WAV. Accepting it would hand ImageSharp a container it cannot
        // decode and turn a bad upload into an exception rather than a 400.
        Assert.Equal(ImageFormat.Webp, MediaFormats.Detect("RIFF\0\0\0\0WEBP"u8));
        Assert.Null(MediaFormats.Detect("RIFF\0\0\0\0AVI "u8));
    }

    [Fact]
    public void Rejects_svg_however_it_is_dressed_up()
    {
        // SVG is XML and can execute script; unlike a raster format it cannot be neutralised by
        // re-encoding, so it must never be detected as an accepted image (ADR-0011).
        Assert.Null(MediaFormats.Detect("<svg xmlns=\"http://www.w3.org/2000/svg\">"u8));
        Assert.Null(MediaFormats.Detect("<?xml version=\"1.0\"?><svg>"u8));
    }

    [Theory]
    [InlineData("MZ\0\0\0\0\0")]           // Windows executable
    [InlineData("%PDF-1.7")]                      // PDF
    [InlineData("PK........")]        // ZIP / docx / jar
    [InlineData("ELF............")]         // Linux executable
    public void Rejects_non_image_content(string content)
        => Assert.Null(MediaFormats.Detect(System.Text.Encoding.Latin1.GetBytes(content)));

    [Fact]
    public void Rejects_content_shorter_than_a_signature()
    {
        Assert.Null(MediaFormats.Detect([]));
        Assert.Null(MediaFormats.Detect([0xFF]));
        Assert.Null(MediaFormats.Detect([0xFF, 0xD8]));
    }

    [Fact]
    public void Keeps_png_and_gif_but_normalises_everything_else_to_jpeg()
    {
        // PNG carries screenshots and diagrams, where JPEG's ringing around text is obvious; an
        // animated GIF re-encoded to JPEG would silently become a single still frame.
        Assert.Equal(ImageFormat.Png, MediaFormats.OutputFor(ImageFormat.Png));
        Assert.Equal(ImageFormat.Gif, MediaFormats.OutputFor(ImageFormat.Gif));
        Assert.Equal(ImageFormat.Jpeg, MediaFormats.OutputFor(ImageFormat.Jpeg));
        Assert.Equal(ImageFormat.Jpeg, MediaFormats.OutputFor(ImageFormat.Webp));
    }
}
