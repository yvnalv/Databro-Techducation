using DataBro.Platform.Results;
using Microsoft.AspNetCore.Http;

namespace DataBro.Modules.Content.Api;

/// <summary>
/// Wraps results in the standard API envelope (docs/API_SPEC.md, docs/ERROR_HANDLING.md).
/// A shared web kernel will replace this once a second module needs it.
/// </summary>
internal static class ApiEnvelope
{
    public static IResult Ok(object? data) => Results.Ok(new { success = true, data });

    public static IResult Fail(Error error) =>
        Results.Json(
            new { success = false, error = new { code = error.Code, message = error.Message } },
            statusCode: StatusFor(error.Code));

    public static IResult From<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : Fail(result.Error);

    public static IResult OkOrNotFound(object? data) =>
        data is null
            ? Fail(Error.NotFound("Resource not found."))
            : Ok(data);

    private static int StatusFor(string code) => code switch
    {
        "not_found" => StatusCodes.Status404NotFound,
        "validation_failed" => StatusCodes.Status400BadRequest,
        "conflict" or "slug_taken" => StatusCodes.Status409Conflict,
        "business_rule_violation" => StatusCodes.Status422UnprocessableEntity,
        "forbidden" => StatusCodes.Status403Forbidden,
        "unauthenticated" => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status400BadRequest,
    };
}
