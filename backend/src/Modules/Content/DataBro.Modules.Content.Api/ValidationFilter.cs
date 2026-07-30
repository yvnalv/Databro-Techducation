using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DataBro.Modules.Content.Api;

/// <summary>
/// Minimal-API endpoint filter that runs FluentValidation on the request body and short-circuits
/// with the standard validation envelope (docs/ERROR_HANDLING.md) before the handler runs.
/// </summary>
internal sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        var model = context.Arguments.OfType<T>().FirstOrDefault();

        if (validator is not null && model is not null)
        {
            var result = await validator.ValidateAsync(model, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                var details = result.Errors
                    .Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
                    .ToArray();

                return Results.Json(
                    new
                    {
                        success = false,
                        error = new
                        {
                            code = "validation_failed",
                            message = "One or more fields are invalid.",
                            details,
                        },
                    },
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }

        return await next(context);
    }
}
