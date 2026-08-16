using Amazon.S3;
using Amazon.S3.Model;
using DataBro.Modules.Media.Application;
using Microsoft.Extensions.Options;

namespace DataBro.Modules.Media.Infrastructure.Storage;

/// <summary>
/// S3-compatible object storage (ADR-0011) — MinIO in development, DigitalOcean Spaces in
/// production. One implementation for both: they differ by endpoint and credentials, not by API.
/// </summary>
internal sealed class S3MediaStorage(IAmazonS3 client, IOptions<MediaOptions> options) : IMediaStorage
{
    private readonly MediaOptions _options = options.Value;

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        await client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                // Media is public content behind a CDN; there is nothing to sign for.
                CannedACL = S3CannedACL.PublicRead,
                Headers =
                {
                    // Content-addressed keys never change contents, so a long immutable cache is
                    // both safe and the whole point of storing variants.
                    CacheControl = "public, max-age=31536000, immutable",
                },
            },
            ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
        => await client.DeleteObjectAsync(
            new DeleteObjectRequest { BucketName = _options.Bucket, Key = key }, ct);

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var response = await client.GetObjectAsync(
            new GetObjectRequest { BucketName = _options.Bucket, Key = key }, ct);

        return response.ResponseStream;
    }

    public string UrlFor(string key)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? _options.Endpoint
            : _options.PublicBaseUrl;

        baseUrl = baseUrl.TrimEnd('/');

        // Path-style puts the bucket in the path; virtual-host style has it in the host name, so the
        // URL must not repeat it.
        return _options.UsePathStyle
            ? $"{baseUrl}/{_options.Bucket}/{key}"
            : $"{baseUrl}/{key}";
    }
}
