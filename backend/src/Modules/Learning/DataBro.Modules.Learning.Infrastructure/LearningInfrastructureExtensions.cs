using DataBro.Modules.Learning.Application;
using DataBro.Modules.Learning.Infrastructure.Persistence;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Persistence;
using DataBro.Platform.Persistence.Auditing;
using DataBro.Platform.Persistence.Outbox;
using DataBro.Platform.Messaging;
using DataBro.Modules.Learning.Domain;
using DataBro.Modules.Learning.Infrastructure.Messaging;
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
        services.AddScoped<OutboxInterceptor>();

        services.AddDbContext<LearningDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npg =>
                npg.MigrationsHistoryTable("__ef_migrations_history", LearningDbContext.Schema));
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(
                sp.GetRequiredService<AuditingInterceptor>(),
                sp.GetRequiredService<OutboxInterceptor>());
        });

        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<ILearningPathRepository, LearningPathRepository>();
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        services.AddScoped<IBookmarkRepository, BookmarkRepository>();
        services.AddScoped<CourseService>();
        services.AddScoped<EnrollmentService>();
        services.AddScoped<BookmarkService>();
        services.AddScoped<LearningPathService>();

        // Learning's segment of the cross-module search results (ADR-0014).
        services.AddScoped<IModuleSearch, Persistence.CourseSearch>();

        // The outbox: what may cross the boundary, who listens, and the worker that drains it.
        // The contract name is written by hand and must never change once messages exist under it.
        services.AddSingleton(sp =>
        {
            var registry = new OutboxRegistry();
            registry.Register<CourseCompletedDomainEvent>("learning.course-completed");
            return registry;
        });

        services.AddScoped<IIntegrationEventHandler<CourseCompletedDomainEvent>, CourseCompletedEmailHandler>();
        services.AddScoped<OutboxProcessor<LearningDbContext>>();
        services.AddScoped<LearningOutboxJob>();
        services.AddHostedService<LearningJobsInitializer>();

        services.AddHostedService<LearningInitializer>();

        return services;
    }
}
