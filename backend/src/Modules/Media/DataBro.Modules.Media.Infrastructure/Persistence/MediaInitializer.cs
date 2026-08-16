using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using DataBro.Modules.Media.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataBro.Modules.Media.Infrastructure.Persistence;

/// <summary>
/// In Development, applies pending Media migrations and — when configured — provisions the bucket so
/// a fresh clone self-provisions (never in production, see docs/DEPLOYMENT.md).
/// </summary>
public sealed class MediaInitializer(
    IServiceProvider services,
    IHostEnvironment environment,
    IOptions<MediaOptions> options,
    ILogger<MediaInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return;

        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MediaDbContext>()
            .Database.MigrateAsync(cancellationToken);

        await EnsureBucketAsync(scope.ServiceProvider, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Creates the bucket and opens it for public reads.
    ///
    /// Development only, and gated on an explicit setting: in production the bucket and its access
    /// policy are provisioned deliberately, and an application that can grant itself public-read on
    /// a bucket is an application whose credentials are worth far more if stolen.
    /// </summary>
    private async Task EnsureBucketAsync(IServiceProvider scope, CancellationToken ct)
    {
        if (!options.Value.CreateBucketOnStartup) return;

        var s3 = scope.GetRequiredService<IAmazonS3>();
        var bucket = options.Value.Bucket;

        try
        {
            if (await AmazonS3Util.DoesS3BucketExistV2Async(s3, bucket))
                return;

            await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, ct);

            // Objects are written with a public-read ACL, but MinIO ignores object ACLs unless the
            // bucket policy allows anonymous reads — without this, every image 403s in development
            // while working in production, which is the worst possible way for the two to differ.
            await s3.PutBucketPolicyAsync(
                new PutBucketPolicyRequest
                {
                    BucketName = bucket,
                    Policy = $$"""
                    {
                      "Version": "2012-10-17",
                      "Statement": [{
                        "Effect": "Allow",
                        "Principal": "*",
                        "Action": ["s3:GetObject"],
                        "Resource": ["arn:aws:s3:::{{bucket}}/*"]
                      }]
                    }
                    """,
                },
                ct);

            logger.LogInformation("Created development media bucket {Bucket} with public read access.", bucket);
        }
        catch (Exception ex)
        {
            // Storage being unreachable must not stop the API from booting: every other module works
            // fine without it, and a dev who is not touching media should not be blocked.
            logger.LogWarning(ex, "Could not provision the media bucket {Bucket}. Uploads will fail until it exists.", bucket);
        }
    }
}
