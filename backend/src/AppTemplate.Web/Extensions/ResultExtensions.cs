using AppTemplate.Web.Configurations;

namespace AppTemplate.Web.Extensions;

/// <summary>
/// The only place an <see cref="Ardalis.Result.IResult"/> becomes an HTTP response, so every
/// endpoint reports the same outcome with the same status code and problem details body
/// (ADR 014). Endpoints return <c>Results&lt;Success, ProblemHttpResult&gt;</c>.
/// </summary>
public static class ResultExtensions
{
    public static Results<Ok<TResponse>, ProblemHttpResult> ToOkResult<TValue, TResponse>(
        this Result<TValue> result,
        Func<TValue, TResponse> mapResponse
    ) => result.IsSuccess ? TypedResults.Ok(mapResponse(result.Value)) : result.ToProblem();

    public static Results<Created<TResponse>, ProblemHttpResult> ToCreatedResult<TValue, TResponse>(
        this Result<TValue> result,
        Func<TValue, string> locationBuilder,
        Func<TValue, TResponse> mapResponse
    ) =>
        result.IsSuccess
            ? TypedResults.Created(locationBuilder(result.Value), mapResponse(result.Value))
            : result.ToProblem();

    public static Results<NoContent, ProblemHttpResult> ToNoContentResult(this Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : result.ToProblem();

    public static Results<Accepted, ProblemHttpResult> ToAcceptedResult(this Result result) =>
        result.IsSuccess ? TypedResults.Accepted((string?)null) : result.ToProblem();

    /// <summary>
    /// Maps a failed result to its status code. Messages from <c>Invalid</c>, <c>NotFound</c>
    /// and <c>Conflict</c> are written by use cases for the caller and are passed on.
    /// <c>Error</c>, <c>CriticalError</c> and <c>Unavailable</c> get a generic detail, because
    /// their messages may describe internals. Use cases log those details themselves.
    /// </summary>
    public static ProblemHttpResult ToProblem(this Ardalis.Result.IResult result) =>
        result.Status switch
        {
            ResultStatus.Invalid => TypedResults.Problem(
                ProblemDetailsConfigurations.ValidationProblem(
                    result
                        .ValidationErrors.GroupBy(error =>
                            ProblemDetailsConfigurations.ErrorKey(error.Identifier)
                        )
                        .ToDictionary(
                            group => group.Key,
                            group => group.Select(error => error.ErrorMessage).ToArray()
                        ),
                    httpContext: null
                )
            ),
            ResultStatus.Unauthorized => Problem(StatusCodes.Status401Unauthorized, detail: null),
            ResultStatus.Forbidden => Problem(
                StatusCodes.Status403Forbidden,
                "You don't have permission to perform this operation."
            ),
            ResultStatus.NotFound => Problem(StatusCodes.Status404NotFound, MessagesOf(result)),
            ResultStatus.Conflict => Problem(StatusCodes.Status409Conflict, MessagesOf(result)),
            ResultStatus.Unavailable => Problem(
                StatusCodes.Status503ServiceUnavailable,
                "The service is temporarily unavailable. Please try again later."
            ),
            ResultStatus.Ok or ResultStatus.Created or ResultStatus.NoContent =>
                throw new InvalidOperationException(
                    $"A successful result ({result.Status}) has no problem response."
                ),
            _ => Problem(StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };

    private static string? MessagesOf(Ardalis.Result.IResult result) =>
        result.Errors.Any() ? string.Join("; ", result.Errors) : null;

    // Title and type come from the status code (ProblemDetailsDefaults).
    private static ProblemHttpResult Problem(int statusCode, string? detail) =>
        TypedResults.Problem(detail: detail, statusCode: statusCode);
}
