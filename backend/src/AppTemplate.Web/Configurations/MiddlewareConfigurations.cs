using System.Diagnostics;
using System.Text.Json;
using AppTemplate.ServiceDefaults.Logging;
using Scalar.AspNetCore;

namespace AppTemplate.Web.Configurations;

public static class MiddlewareConfigurations
{
    public static async Task<IApplicationBuilder> UseAppMiddleware(this WebApplication app)
    {
        // Before anything reads the client address: rewrites RemoteIpAddress from
        // X-Forwarded-For, but only for a connection from a configured proxy.
        app.UseForwardedHeaders();

        // Outside the exception handler, so it logs the status the client actually got.
        app.UseDefaultRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            // Unhandled exceptions become a 500 problem details response without exception
            // details; the middleware logs the exception with the request's trace id.
            app.UseExceptionHandler();
            app.UseHsts();
        }

        // Empty error responses (401/403 from authorization, 404 for unmatched routes) get a
        // problem details body too (AddProblemDetails in Program.cs).
        app.UseStatusCodePages();

        app.UseAuthentication();
        // After authentication: callers are partitioned by validated sub, or by address.
        app.UseRateLimiter();
        app.UseAuthorization();

        app.UseFastEndpoints(config =>
        {
            // Validation failures use the same shape as a use case's Invalid result (ADR 009).
            config.Errors.ResponseBuilder = (failures, context, statusCode) =>
                new HttpValidationProblemDetails(
                    failures
                        .GroupBy(failure =>
                            JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName)
                        )
                        .ToDictionary(
                            group => group.Key,
                            group =>
                                group.Select(failure => failure.ErrorMessage).Distinct().ToArray()
                        )
                )
                {
                    Status = statusCode,
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    Extensions = { ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier },
                };
            config.Errors.ProducesMetadataType = typeof(HttpValidationProblemDetails);
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseSwaggerGen(
                options =>
                {
                    options.Path = "/openapi/{documentName}.json";
                },
                settings =>
                {
                    settings.Path = "/swagger";
                    settings.DocumentPath = "/openapi/{documentName}.json";
                }
            );

            app.MapScalarApiReference(options =>
                {
                    options.WithTitle("AppTemplate API");
                    options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
                })
                .AllowAnonymous(); // a browser page, not an API call: no bearer token
        }

        app.UseHttpsRedirection(); // Note this will drop Authorization headers

        return app;
    }
}
