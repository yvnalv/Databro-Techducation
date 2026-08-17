using DataBro.Modules.Content.Application;
using DataBro.Modules.Content.Infrastructure.Persistence;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Persistence;
using DataBro.Platform.Persistence.Auditing;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataBro.Modules.Content.Infrastructure;

/// <summary>Registers the Content module's infrastructure (persistence, repositories, services).</summary>
public static class ContentInfrastructureExtensions
{
    public static IServiceCollection AddContentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing connection string 'Postgres'.");

        // Platform defaults (registered once; safe if another module also registers them).
        // ICurrentUser is scoped — the Identity module replaces NullCurrentUser with the JWT-backed
        // HttpCurrentUser when present.
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddScoped<ICurrentUser, NullCurrentUser>();
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<ContentDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npg =>
                npg.MigrationsHistoryTable("__ef_migrations_history", ContentDbContext.Schema));
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        services.AddScoped<IArticleRepository, ArticleRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IRedirectRepository, RedirectRepository>();
        services.AddScoped<IContentSlugRegistry, ContentSlugRegistry>();
        services.AddScoped<ArticleService>();
        services.AddScoped<TaxonomyService>();
        services.AddScoped<RedirectService>();
        services.AddScoped<ScheduledPublishingJob>();

        services.AddValidatorsFromAssemblyContaining<ArticleService>(ServiceLifetime.Singleton);

        services.AddHostedService<Persistence.ContentInitializer>();
        // Registers the scheduled-publish recurring job (CT-7) where a Hangfire server is running.
        services.AddHostedService<ContentJobsInitializer>();

        return services;
    }
}
