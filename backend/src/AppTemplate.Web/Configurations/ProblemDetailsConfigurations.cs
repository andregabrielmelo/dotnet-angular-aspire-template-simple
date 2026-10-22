using System.Diagnostics;
using System.Text.Json;
using FluentValidation.Results;

namespace AppTemplate.Web.Configurations;

/// <summary>
/// One error contract for every failure: RFC 9457 problem details
/// (<c>application/problem+json</c>) carrying a <c>traceId</c>, whether the failure comes from
/// request validation, a use case result, authentication, an unmatched route or an unhandled
/// exception. Validation failures always use ASP.NET Core's shape, an <c>errors</c> object of
/// camelCase field name to messages, so clients parse one format. See ADR 014.
/// </summary>
public static class ProblemDetailsConfigurations
{
    public const string TraceIdExtension = "traceId";

    private const string ValidationTitle = "One or more validation errors occurred.";
    private const string ValidationType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    public static IServiceCollection AddProblemDetailsConfigurations(
        this IServiceCollection services,
        Microsoft.Extensions.Logging.ILogger logger
    )
    {
        // Used by TypedResults.Problem, the exception handler and status code pages alike.
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions[TraceIdExtension] = TraceIdOf(context.HttpContext)
        );

        logger.LogInformation("{Project} were configured", "Problem details");

        return services;
    }

    /// <summary>The id that links a response to its logs and its trace in the Aspire dashboard.</summary>
    public static string TraceIdOf(HttpContext httpContext) =>
        Activity.Current?.Id ?? httpContext.TraceIdentifier;

    /// <summary>The key a field's errors are reported under, matching the JSON property name.</summary>
    public static string ErrorKey(string? propertyName) =>
        string.IsNullOrEmpty(propertyName)
            ? string.Empty
            : JsonNamingPolicy.CamelCase.ConvertName(propertyName);

    /// <summary>FastEndpoints' validation failures, in the same shape as a use case's <c>Invalid</c> result.</summary>
    public static object FastEndpointsValidationProblem(
        List<ValidationFailure> failures,
        HttpContext httpContext,
        int statusCode
    ) =>
        ValidationProblem(
            failures
                .GroupBy(failure => ErrorKey(failure.PropertyName))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray()
                ),
            httpContext,
            statusCode
        );

    public static HttpValidationProblemDetails ValidationProblem(
        IDictionary<string, string[]> errors,
        HttpContext? httpContext,
        int statusCode = StatusCodes.Status400BadRequest
    )
    {
        var problem = new HttpValidationProblemDetails(errors)
        {
            Status = statusCode,
            Title = ValidationTitle,
            Type = ValidationType,
        };
        if (httpContext is not null)
        {
            problem.Extensions[TraceIdExtension] = TraceIdOf(httpContext);
        }
        return problem;
    }
}
