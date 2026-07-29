using DataBro.Modules.Search.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataBro.Modules.Search.Api;

/// <summary>Composition root for the Search module: DI registration and endpoint mapping.</summary>
public static class SearchModuleExtensions
{
    public static IServiceCollection AddSearchModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSearchInfrastructure(configuration);
        // TODO: register Search application services (handlers, validators).
        return services;
    }

    public static IEndpointRouteBuilder MapSearchModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/search").WithTags("Search");
        group.MapGet("/_ping", () => Results.Ok(new { module = "Search", status = "ok" }));
        return endpoints;
    }
}
