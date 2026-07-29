using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataBro.Modules.Search.Infrastructure;

/// <summary>Registers the Search module's infrastructure services (persistence, external adapters).</summary>
public static class SearchInfrastructureExtensions
{
    public static IServiceCollection AddSearchInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // TODO: register DbContext, repositories, and external integrations for the Search module.
        return services;
    }
}
