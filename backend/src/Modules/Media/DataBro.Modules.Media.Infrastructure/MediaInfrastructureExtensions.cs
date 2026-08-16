using Amazon.Runtime;
using Amazon.S3;
using DataBro.Modules.Media.Application;
using DataBro.Modules.Media.Infrastructure.Directory;
using DataBro.Modules.Media.Infrastructure.Imaging;
using DataBro.Modules.Media.Infrastructure.Jobs;
using DataBro.Modules.Media.Infrastructure.Persistence;
using DataBro.Modules.Media.Infrastructure.Storage;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Persistence;
using DataBro.Platform.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataBro.Modules.Media.Infrastructure;

/// <summary>Registers the Media module's infrastructure (persistence, storage, imaging, jobs).</summary>
public static class MediaInfrastructureExtensions
{
    public static IServiceCollection AddMediaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing connection string 'Postgres'.");

        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddScoped<ICurrentUser, NullCurrentUser>();
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<MediaDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npg =>
                npg.MigrationsHistoryTable("__ef_migrations_history", MediaDbContext.Schema));
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        services.Configure<MediaOptions>(configuration.GetSection(MediaOptions.SectionName));

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MediaOptions>>().Value;

            var config = new AmazonS3Config
            {
                ServiceURL = options.Endpoint,
                ForcePathStyle = options.UsePathStyle,
                AuthenticationRegion = options.Region,
            };

            return new AmazonS3Client(
                new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
        });

        services.AddSingleton<IImageProcessor, ImageSharpProcessor>();
        services.AddScoped<IMediaStorage, S3MediaStorage>();
        services.AddScoped<IMediaAssetRepository, MediaAssetRepository>();
        services.AddScoped<MediaService>();
        services.AddScoped<MediaVariantJob>();

        // Media's implementation of the shared cross-module contract (ADR-0008). Consumers resolve
        // IMediaDirectory and never learn that this module exists.
        services.AddScoped<IMediaDirectory, MediaDirectory>();

        // Where a Hangfire server runs, variants are generated in the background (ADR-0011).
        // Where one does not — integration tests, or a host with the server disabled — they run
        // inline, so behaviour is the same and nothing waits on a queue nobody drains.
        if (configuration.GetValue("Hangfire:EnableServer", true))
            services.AddScoped<IMediaVariantQueue, HangfireMediaVariantQueue>();
        else
            services.AddScoped<IMediaVariantQueue, InlineMediaVariantQueue>();

        services.AddHostedService<MediaInitializer>();

        return services;
    }
}
