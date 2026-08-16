using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace DataBro.Modules.Media.Tests;

/// <summary>
/// Builds real image bytes for the tests to feed in. Real files rather than hand-written byte arrays
/// because the pipeline under test decodes them — a fake header would prove nothing past the sniff.
/// </summary>
internal static class Fixtures
{
    public static byte[] Jpeg(int width = 1600, int height = 900)
    {
        using var image = new Image<Rgba32>(width, height);
        using var buffer = new MemoryStream();
        image.SaveAsJpeg(buffer, new JpegEncoder { Quality = 90 });
        return buffer.ToArray();
    }

    public static byte[] Png(int width = 800, int height = 600)
    {
        using var image = new Image<Rgba32>(width, height);
        using var buffer = new MemoryStream();
        image.SaveAsPng(buffer);
        return buffer.ToArray();
    }

    /// <summary>A JPEG carrying GPS coordinates and a camera model, as a phone photo would.</summary>
    public static byte[] JpegWithExif(int width = 1600, int height = 900)
    {
        using var image = new Image<Rgba32>(width, height);

        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Model, "DataBro Test Camera");
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLatitude, [new Rational(51), new Rational(30), new Rational(0)]);
        exif.SetValue(ExifTag.GPSLongitudeRef, "W");
        exif.SetValue(ExifTag.GPSLongitude, [new Rational(0), new Rational(7), new Rational(39)]);
        image.Metadata.ExifProfile = exif;

        using var buffer = new MemoryStream();
        image.SaveAsJpeg(buffer, new JpegEncoder { Quality = 90 });
        return buffer.ToArray();
    }

    /// <summary>
    /// A file that is a valid PNG *and* carries an HTML payload after the image data — the polyglot
    /// shape that passes a header check and is then dangerous if served back verbatim (ADR-0011).
    /// </summary>
    public static byte[] PolyglotPng()
    {
        var png = Png(64, 64);
        var payload = "<script>alert('xss')</script>"u8.ToArray();

        var combined = new byte[png.Length + payload.Length];
        png.CopyTo(combined, 0);
        payload.CopyTo(combined, png.Length);
        return combined;
    }

    public static Stream Stream(byte[] bytes) => new MemoryStream(bytes);
}
