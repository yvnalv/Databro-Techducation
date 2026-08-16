using DataBro.Modules.Media.Domain;

namespace DataBro.Modules.Media.Application;

// DTOs exchanged with the API layer. `processingStatus` goes over the wire lowercase to match the
// discriminated unions in @databro/types, as Content's DTOs do.

public sealed record MediaVariantDto(string Name, string Url, int Width, int Height);

public sealed record MediaAssetDto(
    Guid Id,
    string Url,
    string FileName,
    string MimeType,
    long ByteSize,
    int Width,
    int Height,
    string AltText,
    string ProcessingStatus,
    string? ProcessingError,
    DateTimeOffset CreatedAt,
    IReadOnlyList<MediaVariantDto> Variants);

/// <summary>The bytes and metadata of one upload, decoupled from ASP.NET's IFormFile.</summary>
public sealed record UploadMediaRequest(
    Stream Content,
    string FileName,
    long ByteSize,
    string? AltText);

public sealed record UpdateMediaRequest(string AltText);

internal static class MediaMapping
{
    public static MediaAssetDto ToDto(this MediaAsset asset, IMediaStorage storage) =>
        new(
            asset.Id,
            storage.UrlFor(asset.StorageKey),
            asset.FileName,
            asset.MimeType,
            asset.ByteSize,
            asset.Width,
            asset.Height,
            asset.AltText,
            asset.ProcessingStatus.ToString().ToLowerInvariant(),
            asset.ProcessingError,
            asset.CreatedAt,
            asset.Variants
                .OrderBy(v => v.Width)
                .Select(v => new MediaVariantDto(v.Name, storage.UrlFor(v.StorageKey), v.Width, v.Height))
                .ToList());
}
