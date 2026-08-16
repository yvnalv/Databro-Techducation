namespace DataBro.Modules.Media.Infrastructure;

/// <summary>
/// Object-storage configuration (ADR-0011). MinIO and DigitalOcean Spaces are both S3-compatible, so
/// development and production differ only by the values here.
/// </summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>S3 endpoint. MinIO: <c>http://minio:9000</c>. Spaces: <c>https://sgp1.digitaloceanspaces.com</c>.</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string Bucket { get; set; } = "databro-media";

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Region. Meaningless to MinIO but required by the AWS SDK's request signer, so it is set to a
    /// placeholder in development rather than left empty.
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Path-style addressing (<c>endpoint/bucket/key</c>) instead of virtual-host style
    /// (<c>bucket.endpoint/key</c>). Required for MinIO on a bare host name; Spaces supports both,
    /// and virtual-host style is the better choice there.
    /// </summary>
    public bool UsePathStyle { get; set; } = true;

    /// <summary>
    /// Public base URL for reading objects, if it differs from <see cref="Endpoint"/> — a CDN origin
    /// in production, or the browser-reachable address in development, where the API talks to
    /// <c>minio:9000</c> over the container network but a reader's browser cannot resolve that name.
    /// Empty means "derive it from the endpoint".
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Create the bucket at startup if missing. Convenient for a fresh dev stack; must stay off in
    /// production, where the bucket and its access policy are provisioned deliberately.
    /// </summary>
    public bool CreateBucketOnStartup { get; set; }
}
