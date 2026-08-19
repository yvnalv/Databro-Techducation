using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DataBro.Modules.Assessment.Infrastructure.Persistence;
using DataBro.Modules.Content.Infrastructure.Persistence;
using DataBro.Modules.Learning.Infrastructure.Persistence;
using DataBro.Modules.Identity.Domain;
using DataBro.Modules.Identity.Infrastructure.Persistence;
using DataBro.Modules.Identity.Infrastructure.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace DataBro.Modules.Learning.Tests;

/// <summary>
/// Boots the real API host against a throwaway PostgreSQL container (Testcontainers), applies
/// migrations for all modules, and seeds RBAC roles. Provides an authenticated-client helper so
/// integration tests can exercise permission-protected endpoints (docs/TESTING.md).
/// </summary>
public sealed class LearningApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Postgres", _db.GetConnectionString());
        // No Hangfire server in tests: they invoke the scheduled-publish job directly, so the host
        // must not stand one up (nor touch Hangfire storage). See ContentJobsInitializer.
        builder.UseSetting("Hangfire:EnableServer", "false");
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _db.StartAsync();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ContentDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<LearningDbContext>().Database.MigrateAsync();
        // The completion gate (AS-9) asks Assessment whether a lesson's quiz has been passed, so the
        // full-host tests need its schema too.
        await scope.ServiceProvider.GetRequiredService<AssessmentDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>().Database.MigrateAsync();
        await IdentitySeeder.EnsureRolesAsync(Services);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>Registers a user, grants the role, logs in, and returns a bearer-authenticated client.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string role)
    {
        var client = CreateClient();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@databro.test";
        const string password = "Password123!";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password, displayName = role });
        register.EnsureSuccessStatusCode();

        using (var scope = Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);

            // Confirmed here because sign-in now requires it. Tests whose subject is *not* the
            // confirmation gate should not each have to walk it, and the gate has its own coverage
            // in AccountRecoveryTests.
            var confirmToken = await users.GenerateEmailConfirmationTokenAsync(user!);
            await users.ConfirmEmailAsync(user!, confirmToken);

            if (role != Roles.Reader)
                await users.AddToRoleAsync(user!, role);
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var root = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement;
        var token = root.GetProperty("data").GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
