using DataBro.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DataBro.Modules.Identity.Infrastructure.Seeding;

/// <summary>
/// Startup task: in Development it applies pending Identity migrations for a self-provisioning dev
/// database (never in production — see docs/DEPLOYMENT.md), and always ensures RBAC roles exist.
/// </summary>
public sealed class IdentityInitializer(IServiceProvider services, IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsDevelopment())
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        await IdentitySeeder.EnsureRolesAsync(services, cancellationToken);

        // Roles first: the admin is assigned one the moment it is created. Development only, and
        // the gate lives here rather than inside the seeder so the decision is visible at the call
        // site — a seeded admin with a documented password is a back door in any other environment.
        if (environment.IsDevelopment())
            await IdentitySeeder.EnsureDevAdminAsync(services, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
