namespace AppTemplate.BackendForFrontend.Configurations;

public static class AntiforgeryHeaderMiddleware
{
    public const string HeaderName = "X-CSRF";

    /// <summary>
    /// CSRF protection for cookie-authenticated calls: API and session endpoints only answer
    /// requests carrying <c>X-CSRF: 1</c>. A cross-site page can't add a custom header without
    /// a CORS preflight, and this host allows no CORS, so only the SPA itself can send it.
    /// </summary>
    public static IApplicationBuilder UseAntiforgeryHeaderCheck(this IApplicationBuilder app) =>
        app.Use(
            async (context, next) =>
            {
                var path = context.Request.Path;
                var requiresHeader =
                    path.StartsWithSegments("/api")
                    || path.StartsWithSegments(BackendForFrontendEndpoints.UserPath);

                if (requiresHeader && context.Request.Headers[HeaderName] != "1")
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                await next(context);
            }
        );
}
