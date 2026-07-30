using DataBro.Platform.Results;
using Microsoft.AspNetCore.Http;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace DataBro.Platform.Web;

/// <summary>
/// Wraps results in the standard API envelope (docs/API_SPEC.md, docs/ERROR_HANDLING.md).
/// Shared by every module's minimal-API endpoints.
/// </summary>
public static class ApiEnvelope
{
    public static IResult Ok(object? data) => HttpResults.Ok(new { success = true, data });

    /// <summary>
    /// A successful response carrying paging information in <c>meta</c> (docs/API_SPEC.md §3), so a
    /// client can render crawlable page links without a second request.
    /// </summary>
    public static IResult OkPaged<T>(PagedResult<T> page) =>
        HttpResults.Ok(new
        {
            success = true,
            data = page.Items,
            meta = new
            {
                page = page.Page,
                pageSize = page.PageSize,
                total = page.Total,
                totalPages = page.TotalPages,
            },
        });

    public static IResult Fail(Error error) =>
        HttpResults.Json(
            new { success = false, error = new { code = error.Code, message = error.Message } },
            statusCode: StatusFor(error.Code));

    public static IResult From<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : Fail(result.Error);

    public static IResult FromEmpty(Result result) =>
        result.IsSuccess ? Ok(new { }) : Fail(result.Error);

    public static IResult OkOrNotFound(object? data) =>
        data is null ? Fail(Error.NotFound("Resource not found.")) : Ok(data);

    public static int StatusFor(string code) => code switch
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
