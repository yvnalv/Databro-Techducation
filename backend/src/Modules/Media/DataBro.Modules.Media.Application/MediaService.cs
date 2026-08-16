using System.Security.Cryptography;
using DataBro.Modules.Media.Domain;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Results;

namespace DataBro.Modules.Media.Application;

/// <summary>
/// Use cases for the Media module (ADR-0011).
///
/// The upload path is the security-critical one, and its order is deliberate: identify by magic
/// bytes, bound by header dimensions, decode, re-encode, hash the output, store, record, enqueue.
/// Each step assumes the previous one has already refused anything hostile.
/// </summary>
public sealed class MediaService(
    IMediaAssetRepository repository,
    IMediaStorage storage,
    IImageProcessor processor,
    IMediaVariantQueue variants,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result<MediaAssetDto>> UploadAsync(
        UploadMediaRequest request, CancellationToken ct = default)
    {
        if (request.ByteSize <= 0)
            return Result.Failure<MediaAssetDto>(Error.Validation("The uploaded file is empty."));

        if (request.ByteSize > MediaLimits.MaxBytes)
            return Result.Failure<MediaAssetDto>(Error.Validation(
                $"The file is larger than the {MediaLimits.MaxBytes / (1024 * 1024)} MB limit."));

        // Buffered because the pipeline reads the bytes three times — sniff, measure, decode — and a
        // request body stream is forward-only. Bounded by the size check above, so this cannot be
        // used to exhaust memory.
        using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, ct);

        if (buffer.Length == 0)
            return Result.Failure<MediaAssetDto>(Error.Validation("The uploaded file is empty."));

        // The declared content type and the file extension are both attacker-controlled, so neither
        // is consulted. The leading bytes are the only thing that says what this actually is.
        var header = new byte[Math.Min(MediaFormats.SignatureLength, buffer.Length)];
        buffer.Position = 0;
        _ = await buffer.ReadAsync(header, ct);

        var source = MediaFormats.Detect(header);
        if (source is null)
            return Result.Failure<MediaAssetDto>(Error.Validation(
                "Only JPEG, PNG, WebP and GIF images can be uploaded."));

        // Dimensions from the header, before any pixels are allocated — the decompression-bomb
        // check. A 100 MB PNG of one flat colour is small on the wire and gigabytes decoded.
        buffer.Position = 0;
        var dimensions = processor.ReadDimensions(buffer);
        if (dimensions is null)
            return Result.Failure<MediaAssetDto>(Error.Validation("The file is not a readable image."));

        var validation = MediaAsset.ValidateUpload(buffer.Length, dimensions.Width, dimensions.Height);
        if (validation.IsFailure)
            return Result.Failure<MediaAssetDto>(validation.Error);

        var output = MediaFormats.OutputFor(source.Value);

        ProcessedImage normalized;
        try
        {
            buffer.Position = 0;
            normalized = processor.Normalize(buffer, output);
        }
        catch (Exception ex)
        {
            // A file that sniffs as an image and then fails to decode is either corrupt or crafted.
            // Either way it is the caller's problem, not a 500.
            return Result.Failure<MediaAssetDto>(Error.Validation(
                $"The image could not be processed: {ex.Message}"));
        }

        var id = Guid.NewGuid();
        var now = clock.UtcNow;
        var key = MediaAsset.BuildStorageKey(id, now, "original", MediaFormats.ExtensionOf(output));

        using (var stored = new MemoryStream(normalized.Bytes))
            await storage.PutAsync(key, stored, MediaFormats.MimeTypeOf(output), ct);

        var asset = MediaAsset.Create(
            id,
            key,
            request.FileName,
            MediaFormats.MimeTypeOf(output),
            normalized.Bytes.LongLength,
            normalized.Width,
            normalized.Height,
            // Hashes the re-encoded bytes, which are what we actually hold. Hashing the upload would
            // describe a file that no longer exists anywhere.
            Convert.ToHexString(SHA256.HashData(normalized.Bytes)).ToLowerInvariant(),
            currentUser.UserId ?? Guid.Empty,
            request.AltText);

        await repository.AddAsync(asset, ct);
        await repository.SaveChangesAsync(ct);

        // Enqueued after the commit, so the job cannot start against a row that is not there yet.
        variants.Enqueue(asset.Id);

        return Result.Success(asset.ToDto(storage));
    }

    public async Task<MediaAssetDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var asset = await repository.GetByIdAsync(id, ct);
        return asset?.ToDto(storage);
    }

    public async Task<PagedResult<MediaAssetDto>> ListAsync(PageRequest page, CancellationToken ct = default)
    {
        var (items, total) = await repository.ListAsync(page.Skip, page.PageSize, ct);
        return new PagedResult<MediaAssetDto>(
            items.Select(a => a.ToDto(storage)).ToList(), page.Page, page.PageSize, total);
    }

    public async Task<Result<MediaAssetDto>> UpdateAsync(
        Guid id, UpdateMediaRequest request, CancellationToken ct = default)
    {
        var asset = await repository.GetByIdAsync(id, ct);
        if (asset is null)
            return Result.Failure<MediaAssetDto>(Error.NotFound("Media asset not found."));

        asset.Describe(request.AltText);
        await repository.SaveChangesAsync(ct);

        return Result.Success(asset.ToDto(storage));
    }

    /// <summary>
    /// Soft-deletes an asset. The stored objects are deliberately <b>left in place</b>: an article
    /// published last year may still reference this id, and physically removing the bytes would put
    /// a hole in a page that was fine a moment ago. Rule 12 (never physically delete content) applies
    /// to the bytes as much as to the row; reclaiming orphans is a sweep that can only run once
    /// something tracks which content references which asset.
    /// </summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var asset = await repository.GetByIdAsync(id, ct);
        if (asset is null)
            return Result.Failure(Error.NotFound("Media asset not found."));

        asset.IsDeleted = true;
        asset.DeletedAt = clock.UtcNow;
        asset.DeletedBy = currentUser.UserId;

        await repository.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Generates the responsive variants for an asset (ADR-0011). Invoked by the background job, and
    /// idempotent — a retry replaces the variant set rather than appending to it.
    /// </summary>
    public async Task GenerateVariantsAsync(Guid assetId, CancellationToken ct = default)
    {
        var asset = await repository.GetByIdAsync(assetId, ct);
        if (asset is null) return;

        try
        {
            await using var original = await storage.OpenReadAsync(asset.StorageKey, ct);
            using var buffer = new MemoryStream();
            await original.CopyToAsync(buffer, ct);

            var format = MediaFormats.OutputFor(FormatOf(asset.MimeType));
            var generated = new List<MediaVariant>();

            foreach (var width in MediaLimits.VariantWidths)
            {
                // Never upscale: a 640px original does not become sharper by being written out at
                // 1920px, it just costs the reader bytes to receive a blurrier picture.
                if (width >= asset.Width) continue;

                buffer.Position = 0;
                var resized = processor.Resize(buffer, width, format);

                var key = MediaAsset.BuildStorageKey(
                    asset.Id, asset.CreatedAt, width.ToString(), MediaFormats.ExtensionOf(format));

                using (var stream = new MemoryStream(resized.Bytes))
                    await storage.PutAsync(key, stream, MediaFormats.MimeTypeOf(format), ct);

                generated.Add(new MediaVariant(
                    Guid.NewGuid(), asset.Id, width.ToString(), key,
                    resized.Width, resized.Height, resized.Bytes.LongLength));
            }

            // An asset narrower than every variant width is Ready with no variants, which is correct:
            // there is nothing to offer beyond the original, and leaving it Pending forever would be
            // a lie.
            asset.SetVariants(generated);
        }
        catch (Exception ex)
        {
            asset.FailProcessing(ex.Message);
        }

        await repository.SaveChangesAsync(ct);
    }

    private static ImageFormat FormatOf(string mimeType) => mimeType switch
    {
        "image/png" => ImageFormat.Png,
        "image/gif" => ImageFormat.Gif,
        "image/webp" => ImageFormat.Webp,
        _ => ImageFormat.Jpeg,
    };
}

/// <summary>
/// Hands an asset to the background worker for variant generation. A port rather than a direct
/// Hangfire call so the Application layer does not depend on the scheduler — the same reason Content
/// keeps its scheduled-publish job behind one.
/// </summary>
public interface IMediaVariantQueue
{
    void Enqueue(Guid assetId);
}
