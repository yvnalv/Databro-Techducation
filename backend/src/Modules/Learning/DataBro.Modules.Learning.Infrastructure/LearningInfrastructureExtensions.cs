using DataBro.Modules.Learning.Application;
using DataBro.Modules.Learning.Infrastructure.Persistence;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Persistence;
using DataBro.Platform.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataBro.Modules.Learning.Infrastructure;

/// <summary>Registers the Learning module's infrastructure (persistence, repositories, services).</summary>
public static class LearningInfrastructureExtensions
{
    public static IServiceCollection AddLearningInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing connection string 'Postgres'.");

        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddScoped<ICurrentUser, NullCurrentUser>();
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<LearningDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npg =>
                npg.MigrationsHistoryTable("__ef_migrations_history", LearningDbContext.Schema));
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<ILearningPathRepository, LearningPathRepository>();
        services.AddScoped<CourseService>();

        // Learning's segment of the cross-module search results (ADR-0014).
        services.AddScoped<IModuleSearch, Persistence.CourseSearch>();

        services.AddHostedService<LearningInitializer>();

        return services;
    }
}
