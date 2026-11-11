using Scalar.AspNetCore;

namespace AppTemplate.Web.Configurations;

public static class MiddlewareConfigurations
{
    public static async Task<IApplicationBuilder> UseAppMiddleware(this WebApplication app)
    {
        // First: the client address and scheme from a known proxy (ForwardedHeadersSettings),
        // before anything reads them.
        app.UseForwardedHeaders();

        // Every request is HTTPS (ADR 016): the API listens on HTTPS only, and behind a host that
        // terminates TLS the forwarded scheme decides. A plain HTTP request is redirected.
        app.UseHttpsRedirection();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            // FastEndpoints' handler: logs the exception, and answers 500 without its message.
            app.UseDefaultExceptionHandler(useGenericReason: true);
            app.UseHsts();
        }

        // The rate limiter runs after authentication, so it can count a signed-in caller by id;
        // then a signed-in session's unsafe requests need an antiforgery token (CSRF, ADR 015).
        app.UseAuthentication().UseRateLimiter().UseSessionAntiforgery().UseAuthorization();

        app.UseFastEndpoints();

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
                .AllowAnonymous(); // a browser page, open before signing in
        }

        return app;
    }
}
