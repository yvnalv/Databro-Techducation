using DataBro.Modules.Media.Application;
using DataBro.Modules.Media.Domain;
using DataBro.Modules.Media.Infrastructure.Imaging;
using SixLabors.ImageSharp;

namespace DataBro.Modules.Media.Tests;

/// <summary>
/// The processing adapter (ADR-0011). Runs against real ImageSharp because the properties under
/// test — that metadata is gone, that a polyglot's payload does not survive — are properties of the
/// actual decode/encode round trip, not of an interface.
/// </summary>
public class ImageSharpProcessorTests
{
    private static IImageProcessor Processor() => new ImageSharpProcessor();

    [Fact]
    public void Reads_dimensions_without_decoding()
    {
        var dimensions = Processor().ReadDimensions(Fixtures.Stream(Fixtures.Jpeg(1234, 567)));

        Assert.NotNull(dimensions);
        Assert.Equal(1234, dimensions.Width);
        Assert.Equal(567, dimensions.Height);
    }

    [Fact]
    public void Returns_null_dimensions_for_unreadable_content()
    {
        // Must be null, not an exception: this is how a crafted file becomes a 400 instead of a 500.
        Assert.Null(Processor().ReadDimensions(Fixtures.Stream("not an image at all"u8.ToArray())));
    }

    [Fact]
    public void Strips_exif_including_gps_coordinates()
    {
        var withExif = Fixtures.JpegWithExif();

        // The fixture really does carry the metadata, so the assertion below means something.
        using (var before = Image.Load(Fixtures.Stream(withExif)))
        {
            Assert.NotNull(before.Metadata.ExifProfile);
            Assert.NotEmpty(before.Metadata.ExifProfile!.Values);
        }

        var normalized = Processor().Normalize(Fixtures.Stream(withExif), ImageFormat.Jpeg);

        using var after = Image.Load(Fixtures.Stream(normalized.Bytes));
        Assert.True(after.Metadata.ExifProfile is null || after.Metadata.ExifProfile.Values.Count == 0);
    }

    [Fact]
    public void Re_encoding_discards_a_polyglot_payload()
    {
        // The central claim of ADR-0011: validating and storing the original would keep the script
        // tag; decoding to pixels and re-encoding cannot carry it, because it was never pixels.
        var polyglot = Fixtures.PolyglotPng();
        Assert.Contains("<script>", System.Text.Encoding.Latin1.GetString(polyglot));

        var normalized = Processor().Normalize(Fixtures.Stream(polyglot), ImageFormat.Png);

        Assert.DoesNotContain("<script>", System.Text.Encoding.Latin1.GetString(normalized.Bytes));
    }

    [Fact]
    public void Resize_preserves_the_aspect_ratio()
    {
        var resized = Processor().Resize(Fixtures.Stream(Fixtures.Jpeg(1600, 900)), 640, ImageFormat.Jpeg);

        Assert.Equal(640, resized.Width);
        Assert.Equal(360, resized.Height); // 1600x900 is 16:9
    }

    [Fact]
    public void Resize_produces_smaller_output_than_the_original()
    {
        var original = Fixtures.Jpeg(1920, 1080);
        var resized = Processor().Resize(Fixtures.Stream(original), 640, ImageFormat.Jpeg);

        Assert.True(resized.Bytes.Length < original.Length,
            $"Expected the 640px variant to be smaller than the 1920px original, got {resized.Bytes.Length} vs {original.Length}.");
    }

    [Fact]
    public void Normalize_can_change_format()
    {
        var normalized = Processor().Normalize(Fixtures.Stream(Fixtures.Png(200, 100)), ImageFormat.Jpeg);

        Assert.Equal(ImageFormat.Jpeg, normalized.Format);
        Assert.Equal(ImageFormat.Jpeg, MediaFormats.Detect(normalized.Bytes));
    }
}
