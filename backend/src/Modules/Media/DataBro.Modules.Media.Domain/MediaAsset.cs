using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Media.Domain;

/// <summary>
/// A stored image and its rendered sizes (ADR-0011).
///
/// The aggregate owns its variants: they are meaningless on their own and are always replaced as a
/// set, never edited individually. <see cref="StorageKey"/> is generated here rather than accepted
/// from a caller — a client-supplied path is a directory-traversal bug waiting to happen.
/// </summary>
public sealed class MediaAsset : AggregateRoot
{
    private readonly List<MediaVariant> _variants = [];

    /// <summary>Object-storage key of the original. Ours, never the client's filename.</summary>
    public string StorageKey { get; private set; } = string.Empty;

    /// <summary>The uploader's filename, kept for display in the picker only.</summary>
    public string FileName { get; private set; } = string.Empty;

    public string MimeType { get; private set; } = string.Empty;
    public long ByteSize { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>
    /// SHA-256 of the <b>stored</b> bytes, not the uploaded ones — after re-encoding, the upload is
    /// no longer what we hold, so hashing the input would describe a file that does not exist.
    /// </summary>
    public string Checksum { get; private set; } = string.Empty;

    public string AltText { get; private set; } = string.Empty;
    public MediaProcessingStatus ProcessingStatus { get; private set; } = MediaProcessingStatus.Pending;

    /// <summary>Why variant generation failed, for the CMS to show. Null unless <c>Failed</c>.</summary>
    public string? ProcessingError { get; private set; }

    public Guid UploadedBy { get; private set; }

    public IReadOnlyList<MediaVariant> Variants => _variants.AsReadOnly();

    private MediaAsset() { } // EF

    public static MediaAsset Create(
        Guid id,
        string storageKey,
        string fileName,
        string mimeType,
        long byteSize,
        int width,
        int height,
        string checksum,
        Guid uploadedBy,
        string? altText = null) =>
        new()
        {
            Id = id,
            StorageKey = storageKey,
            FileName = Truncate(fileName, MediaLimits.MaxFileNameLength),
            MimeType = mimeType,
            ByteSize = byteSize,
            Width = width,
            Height = height,
            Checksum = checksum,
            UploadedBy = uploadedBy,
            AltText = Truncate(altText?.Trim() ?? string.Empty, MediaLimits.MaxAltTextLength),
            ProcessingStatus = MediaProcessingStatus.Pending,
        };

    /// <summary>
    /// Alt text is editable after upload, and deliberately so: it is an accessibility obligation that
    /// an author frequently gets right only once the image is in context.
    /// </summary>
    public void Describe(string altText)
        => AltText = Truncate(altText?.Trim() ?? string.Empty, MediaLimits.MaxAltTextLength);

    /// <summary>
    /// Reconciles the variant set to <paramref name="variants"/> and marks the asset ready.
    ///
    /// Updated in place by name rather than cleared and re-added, matching how
    /// <c>Article.SetTags</c> handles the same problem. Two reasons: the job is idempotent, so a
    /// retry must converge on one correct set rather than accumulate duplicates — and deletes here
    /// are turned into <em>soft</em> deletes by the auditing interceptor, so a clear-and-re-add
    /// would leave the old rows in the table and collide with the unique index on
    /// <c>(media_asset_id, name)</c>.
    /// </summary>
    public void SetVariants(IEnumerable<MediaVariant> variants)
    {
        var target = variants.ToList();

        _variants.RemoveAll(existing => target.All(v => v.Name != existing.Name));

        foreach (var variant in target)
        {
            var existing = _variants.FirstOrDefault(v => v.Name == variant.Name);
            if (existing is null)
                _variants.Add(variant);
            else
                existing.UpdateFrom(variant);
        }

        ProcessingStatus = MediaProcessingStatus.Ready;
        ProcessingError = null;
    }

    /// <summary>
    /// Records that variant generation failed. The asset stays usable at full size — a failed resize
    /// must not cost an author their upload.
    /// </summary>
    public void FailProcessing(string error)
    {
        ProcessingStatus = MediaProcessingStatus.Failed;
        ProcessingError = Truncate(error, 1000);
    }

    /// <summary>
    /// Generates the storage key for an asset or one of its variants (ADR-0011).
    ///
    /// Date-partitioned so a bucket listing stays navigable, and keyed by the asset's own id so two
    /// files with the same name cannot collide. <paramref name="variant"/> is <c>original</c> or a
    /// width name; nothing in the key comes from the uploader.
    /// </summary>
    public static string BuildStorageKey(Guid assetId, DateTimeOffset uploadedAt, string variant, string extension)
        => $"media/{uploadedAt:yyyy}/{uploadedAt:MM}/{assetId:N}/{variant}.{extension}";

    /// <summary>Validates upload metadata that the domain can judge without touching bytes.</summary>
    public static Result ValidateUpload(long byteSize, int width, int height)
    {
        if (byteSize <= 0)
            return Result.Failure(Error.Validation("The uploaded file is empty."));

        if (byteSize > MediaLimits.MaxBytes)
            return Result.Failure(Error.Validation(
                $"The file is larger than the {MediaLimits.MaxBytes / (1024 * 1024)} MB limit."));

        if (width <= 0 || height <= 0)
            return Result.Failure(Error.Validation("The file is not a readable image."));

        if (width > MediaLimits.MaxDimension || height > MediaLimits.MaxDimension)
            return Result.Failure(Error.Validation(
                $"The image is larger than {MediaLimits.MaxDimension}px on a side."));

        // Checked separately from the per-side cap: 11,000 x 11,000 passes that and is still 121
        // megapixels (ADR-0011, decompression bombs).
        if ((long)width * height > MediaLimits.MaxPixels)
            return Result.Failure(Error.Validation("The image has too many pixels to process."));

        return Result.Success();
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}

/// <summary>
/// One rendered size of a <see cref="MediaAsset"/>. Owned by the asset; never referenced directly.
/// </summary>
public sealed class MediaVariant : Entity
{
    public Guid MediaAssetId { get; private set; }

    /// <summary>The variant's name, which is its width — "640", "960". Also part of its key.</summary>
    public string Name { get; private set; } = string.Empty;

    public string StorageKey { get; private set; } = string.Empty;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public long ByteSize { get; private set; }

    private MediaVariant() { } // EF

    public MediaVariant(Guid id, Guid mediaAssetId, string name, string storageKey, int width, int height, long byteSize)
    {
        Id = id;
        MediaAssetId = mediaAssetId;
        Name = name;
        StorageKey = storageKey;
        Width = width;
        Height = height;
        ByteSize = byteSize;
    }

    /// <summary>
    /// Copies the regenerated values onto an existing row, keeping its identity. Called only by
    /// <see cref="MediaAsset.SetVariants"/> — a variant is never edited from outside the aggregate.
    /// </summary>
    internal void UpdateFrom(MediaVariant other)
    {
        StorageKey = other.StorageKey;
        Width = other.Width;
        Height = other.Height;
        ByteSize = other.ByteSize;
    }
}
