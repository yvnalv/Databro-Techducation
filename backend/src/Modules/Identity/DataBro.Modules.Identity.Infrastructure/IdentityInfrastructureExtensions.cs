using System.Security.Claims;
using System.Text;
using DataBro.Modules.Identity.Application;
using DataBro.Modules.Identity.Infrastructure.Auth;
using DataBro.Modules.Identity.Infrastructure.Auth.External;
using DataBro.Modules.Identity.Infrastructure.Authorization;
using DataBro.Modules.Identity.Infrastructure.Directory;
using DataBro.Modules.Identity.Infrastructure.Persistence;
using DataBro.Modules.Identity.Infrastructure.Security;
using DataBro.Modules.Identity.Infrastructure.Seeding;
using DataBro.Platform.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace DataBro.Modules.Identity.Infrastructure;

/// <summary>Registers the Identity module: persistence, ASP.NET Identity, JWT auth, and RBAC.</summary>
public static class IdentityInfrastructureExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing connection string 'Postgres'.");

        services.AddDbContext<IdentityModuleDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npg =>
                npg.MigrationsHistoryTable("__ef_migrations_history", IdentityModuleDbContext.Schema));
            options.UseSnakeCaseNamingConvention();
        });

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                // Deliberately left false, and it is **not** the control. Login goes through
                // `UserManager.CheckPasswordAsync`, not `SignInManager`, so this setting is never
                // consulted — it read like the switch for months while enforcing nothing. The real
                // check is explicit in AuthService.LoginAsync, driven by
                // `Identity:Emails:RequireConfirmedEmail`.
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<IdentityModuleDbContext>()
            .AddDefaultTokenProviders();

        // JWT bearer authentication.
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(jwtSection);
        var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role,
                };
            });

        // Permission-based authorization (perm:{Permission} policies).
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // Ambient current user from the JWT (replaces NullCurrentUser).
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddSingleton<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // ---- Social login (ADR-0019) ----
        // Base URLs bind from the ExternalAuth section; the client secrets bind from flat environment
        // variables (dev: .env via DotNetEnv; deployed: the secret store) so a secret never sits in a
        // config file. IDistributedCache — the handoff-code store's backing — is registered by the host.
        services.Configure<ExternalAuthOptions>(configuration.GetSection(ExternalAuthOptions.SectionName));
        services.Configure<GoogleOAuthOptions>(o =>
        {
            o.ClientId = configuration["GOOGLE_CLIENT_ID"] ?? string.Empty;
            o.ClientSecret = configuration["GOOGLE_CLIENT_SECRET"] ?? string.Empty;
        });
        services.Configure<GitHubOAuthOptions>(o =>
        {
            o.ClientId = configuration["GITHUB_CLIENT_ID"] ?? string.Empty;
            o.ClientSecret = configuration["GITHUB_CLIENT_SECRET"] ?? string.Empty;
        });

        services.AddHttpClient();
        services.AddSingleton<OAuthStateProtector>();
        services.AddScoped<IAuthCodeStore, DistributedCacheAuthCodeStore>();
        services.AddScoped<IExternalIdentityProvider, GoogleProvider>();
        services.AddScoped<IExternalIdentityProvider, GitHubProvider>();
        services.AddScoped<IExternalAuthService, ExternalAuthService>();

        // Identity's outward-facing contract for other modules (ADR-0008). Registered against the
        // shared Platform abstraction so consumers never reference this module.
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IUserContacts, UserContacts>();

        // The transport itself is registered once by the host (Platform.Email); Identity only says
        // what its own emails look like.
        services.Configure<IdentityEmailOptions>(configuration.GetSection(IdentityEmailOptions.SectionName));
        services.AddScoped<IIdentityEmails, IdentityEmails>();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        services.AddHostedService<IdentityInitializer>();

        return services;
    }
}
