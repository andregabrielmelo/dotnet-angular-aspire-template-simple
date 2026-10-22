using Microsoft.Net.Http.Headers;

namespace AppTemplate.Web.Configurations;

public static class SecurityHeadersConfigurations
{
    /// <summary>
    /// The API only returns JSON and problem details, so it needs the minimal set: no MIME
    /// sniffing anywhere, and <c>Cache-Control: no-store</c> on responses for a signed-in caller
    /// so a browser or shared proxy never keeps one user's data. Server-side output caching
    /// (<c>AuthorizedSharedResponsePolicy</c>) is unaffected: it never reads this header. The
    /// HTML-oriented headers (CSP, frame-ancestors, Referrer-Policy) belong to the backend for
    /// frontend, which serves the SPA. Response compression is deliberately not enabled
    /// (BREACH; see best-practices.md).
    /// </summary>
    public static IApplicationBuilder UseApiSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(
            async (context, next) =>
            {
                context.Response.OnStarting(() =>
                {
                    var headers = context.Response.Headers;
                    headers.XContentTypeOptions = "nosniff";

                    if (
                        context.User.Identity?.IsAuthenticated == true
                        && !headers.ContainsKey(HeaderNames.CacheControl)
                    )
                    {
                        headers.CacheControl = "no-store";
                    }

                    return Task.CompletedTask;
                });

                await next(context);
            }
        );
}
