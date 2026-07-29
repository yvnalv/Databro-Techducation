using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataBro.Modules.Identity.Infrastructure;

/// <summary>Registers the Identity module's infrastructure services (persistence, external adapters).</summary>
public static class IdentityInfrastructureExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // TODO: register DbContext, repositories, and external integrations for the Identity module.
        return services;
    }
}
