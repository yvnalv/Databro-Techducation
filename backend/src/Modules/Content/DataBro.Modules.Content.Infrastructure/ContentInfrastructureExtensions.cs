using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataBro.Modules.Content.Infrastructure;

/// <summary>Registers the Content module's infrastructure services (persistence, external adapters).</summary>
public static class ContentInfrastructureExtensions
{
    public static IServiceCollection AddContentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // TODO: register DbContext, repositories, and external integrations for the Content module.
        return services;
    }
}
