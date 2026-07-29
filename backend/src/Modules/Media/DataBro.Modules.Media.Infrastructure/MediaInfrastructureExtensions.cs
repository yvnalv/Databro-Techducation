using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataBro.Modules.Media.Infrastructure;

/// <summary>Registers the Media module's infrastructure services (persistence, external adapters).</summary>
public static class MediaInfrastructureExtensions
{
    public static IServiceCollection AddMediaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // TODO: register DbContext, repositories, and external integrations for the Media module.
        return services;
    }
}
