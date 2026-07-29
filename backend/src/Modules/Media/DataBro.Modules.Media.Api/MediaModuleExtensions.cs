using DataBro.Modules.Media.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataBro.Modules.Media.Api;

/// <summary>Composition root for the Media module: DI registration and endpoint mapping.</summary>
public static class MediaModuleExtensions
{
    public static IServiceCollection AddMediaModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMediaInfrastructure(configuration);
        // TODO: register Media application services (handlers, validators).
        return services;
    }

    public static IEndpointRouteBuilder MapMediaModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/media").WithTags("Media");
        group.MapGet("/_ping", () => Results.Ok(new { module = "Media", status = "ok" }));
        return endpoints;
    }
}
