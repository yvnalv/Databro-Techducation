using DataBro.Modules.Content.Application;
using DataBro.Modules.Content.Infrastructure.Persistence;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Persistence;
using DataBro.Platform.Persistence.Auditing;
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
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<ICurrentUser, NullCurrentUser>();
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<ContentDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npg =>
                npg.MigrationsHistoryTable("__ef_migrations_history", ContentDbContext.Schema));
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        services.AddScoped<IArticleRepository, ArticleRepository>();
        services.AddScoped<ArticleService>();

        return services;
    }
}
