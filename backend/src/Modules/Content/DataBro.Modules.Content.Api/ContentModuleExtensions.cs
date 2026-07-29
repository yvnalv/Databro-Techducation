using DataBro.Modules.Content.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataBro.Modules.Content.Api;

/// <summary>Composition root for the Content module: DI registration and endpoint mapping.</summary>
public static class ContentModuleExtensions
{
    public static IServiceCollection AddContentModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddContentInfrastructure(configuration);
        // TODO: register Content application services (handlers, validators).
        return services;
    }

    public static IEndpointRouteBuilder MapContentModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/content").WithTags("Content");
        group.MapGet("/_ping", () => Results.Ok(new { module = "Content", status = "ok" }));
        return endpoints;
    }
}
