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

        // Early, so its OnStarting callback also covers error and status-code-page responses.
        app.UseApiSecurityHeaders();

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
        // problem details body too. See ProblemDetailsConfigurations.
        app.UseStatusCodePages();

        app.UseAuthentication();
        // After authentication: callers are partitioned by validated sub, or by address.
        app.UseRateLimiter();
        app.UseAuthorization();
        // Inside the status code pages middleware, so a timeout's 504 gets a problem body.
        app.UseRequestTimeouts();

        app.UseFastEndpoints(config =>
        {
            // Routes become /v{n}/..., from each endpoint's explicit Version(n). There is no
            // default version, so an endpoint that forgets Version() stays unversioned and
            // ApiVersioningTests fails (ADR 015).
            config.Versioning.Prefix = ApiVersions.Prefix;
            config.Versioning.PrependToRoute = true;
            config.Errors.ResponseBuilder =
                ProblemDetailsConfigurations.FastEndpointsValidationProblem;
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
