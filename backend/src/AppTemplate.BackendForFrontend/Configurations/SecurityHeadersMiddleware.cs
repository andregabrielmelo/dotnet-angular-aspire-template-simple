using Microsoft.Net.Http.Headers;

namespace AppTemplate.BackendForFrontend.Configurations;

public static class SecurityHeadersMiddleware
{
    /// <summary>
    /// The SPA's policy. Scripts only load from this origin, with no inline scripts and no
    /// eval. Styles allow <c>'unsafe-inline'</c> because Angular injects component styles as
    /// <c>&lt;style&gt;</c> elements at runtime. <c>connect-src 'self'</c> covers the API, the
    /// session endpoints and, in Development, the dev server's same-origin HMR WebSocket.
    /// Signing in is a top-level navigation to Keycloak, which CSP doesn't restrict.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; "
        + "script-src 'self'; "
        + "style-src 'self' 'unsafe-inline'; "
        + "img-src 'self' data:; "
        + "font-src 'self'; "
        + "connect-src 'self'; "
        + "object-src 'none'; "
        + "base-uri 'self'; "
        + "form-action 'self'; "
        + "frame-ancestors 'none'";

    /// <summary>
    /// Every response gets <c>nosniff</c> and a referrer policy. HTML documents (the SPA,
    /// whether from <c>wwwroot</c> or proxied from the Angular dev server) also get the CSP and
    /// <c>X-Frame-Options</c>, the legacy equivalent of <c>frame-ancestors 'none'</c>. A header
    /// a proxied response already carries is left as it is.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(
            async (context, next) =>
            {
                context.Response.OnStarting(() =>
                {
                    var headers = context.Response.Headers;
                    headers.TryAdd(HeaderNames.XContentTypeOptions, "nosniff");
                    headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");

                    if (
                        context.Response.ContentType?.StartsWith(
                            "text/html",
                            StringComparison.OrdinalIgnoreCase
                        ) == true
                    )
                    {
                        headers.TryAdd(HeaderNames.ContentSecurityPolicy, ContentSecurityPolicy);
                        headers.TryAdd(HeaderNames.XFrameOptions, "DENY");
                    }

                    return Task.CompletedTask;
                });

                await next(context);
            }
        );
}
