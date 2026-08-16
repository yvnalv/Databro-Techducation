using DataBro.Modules.Identity.Domain;
using DataBro.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DataBro.Modules.Identity.Infrastructure.Seeding;

/// <summary>Ensures the RBAC roles exist. Idempotent; safe to run on every startup.</summary>
public static class IdentitySeeder
{
    /// <summary>
    /// The development administrator. Credentials are intentionally memorable and intentionally
    /// weak — this account exists only where <see cref="IHostEnvironment.IsDevelopment"/> is true,
    /// and seeding it anywhere else would be handing out an admin login.
    /// </summary>
    public const string DevAdminEmail = "admin@databro.local";

    public const string DevAdminPassword = "Databro-Dev-1!";

    public static async Task EnsureRolesAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }
    }

    /// <summary>
    /// Seeds a known Admin account for local development.
    ///
    /// The CMS had no way in except an account created by a script, whose address was a timestamp —
    /// so signing in meant going and finding the string first. This is a development ergonomics
    /// fix, and it is why the caller must gate it on the environment rather than this method
    /// deciding for itself: a seeded admin with a published password is a back door anywhere else.
    ///
    /// Idempotent. If the account already exists it is left alone, including its password, so a
    /// local change is not undone on every restart.
    /// </summary>
    public static async Task EnsureDevAdminAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (await users.FindByEmailAsync(DevAdminEmail) is not null)
            return;

        var admin = new ApplicationUser
        {
            UserName = DevAdminEmail,
            Email = DevAdminEmail,
            DisplayName = "DataBro Admin",
            // Confirmed on creation: email transport is not wired, so an unconfirmed account would
            // be unusable the moment confirmation is enforced.
            EmailConfirmed = true,
        };

        var created = await users.CreateAsync(admin, DevAdminPassword);
        if (!created.Succeeded)
            return;

        await users.AddToRoleAsync(admin, Roles.Admin);
    }
}
