using DataBro.Modules.Assessment.Application;
using DataBro.Modules.Assessment.Infrastructure.Persistence;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Persistence;
using DataBro.Platform.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DataBro.Modules.Assessment.Infrastructure;

/// <summary>Registers the Assessment module's infrastructure.</summary>
public static class AssessmentInfrastructureExtensions
{
    public static IServiceCollection AddAssessmentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing connection string 'Postgres'.");

        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddScoped<ICurrentUser, NullCurrentUser>();
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<AssessmentDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npg =>
                npg.MigrationsHistoryTable("__ef_migrations_history", AssessmentDbContext.Schema));
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        // No outbox here yet: QuizAttemptSubmitted has no consumer, and adding a queue with nothing
        // reading it is the speculative move ADR-0017 declined twice before being built.

        services.AddScoped<IQuizRepository, QuizRepository>();
        services.AddScoped<IQuizAttemptRepository, QuizAttemptRepository>();
        services.AddScoped<QuizService>();
        services.AddScoped<AttemptService>();

        services.AddHostedService<AssessmentInitializer>();

        return services;
    }
}

/// <summary>Applies migrations at startup in development, mirroring the other modules.</summary>
internal sealed class AssessmentInitializer(IServiceProvider services, IHostEnvironment environment)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return;

        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AssessmentDbContext>()
            .Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
