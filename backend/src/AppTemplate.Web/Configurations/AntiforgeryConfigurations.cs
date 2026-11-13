using AppTemplate.Core.Interfaces;
using Microsoft.AspNetCore.Antiforgery;

namespace AppTemplate.Web.Configurations;

/// <summary>
/// CSRF protection for the session cookie (ADR 015), with ASP.NET Core antiforgery and Angular's
/// built-in XSRF support. <c>GET /auth/antiforgery-token</c> sets <see cref="RequestTokenCookieName"/>,
/// which the SPA can read and HttpClient sends back as <see cref="HeaderName"/>; every unsafe
/// request a session makes must carry it. Tokens are bound to the signed-in user, so the SPA
/// fetches a new one after signing in. Anonymous requests carry no credential to forge, so they
/// aren't checked.
/// </summary>
public static class AntiforgeryConfigurations
{
    /// <summary>The readable cookie Angular's HttpClient takes the token from (its default name).</summary>
    public const string RequestTokenCookieName = "XSRF-TOKEN";

    /// <summary>The header Angular's HttpClient sends the token in (its default name).</summary>
    public const string HeaderName = "X-XSRF-TOKEN";

    public static IServiceCollection AddAntiforgeryConfigurations(
        this IServiceCollection services,
        ILoggerAdapter logger
    )
    {
        services.AddAntiforgery(options =>
        {
            options.HeaderName = HeaderName;
            // The HttpOnly half of the token pair.
            options.Cookie.Name = "apptemplate_antiforgery";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            // UseSecurityHeaders sends X-Frame-Options: DENY on every response.
            options.SuppressXFrameOptionsHeader = true;
        });

        logger.LogInformation("{Project} was configured", "Antiforgery");
        return services;
    }

    /// <summary>The readable cookie's options: the session cookie's, except that scripts can read it.</summary>
    public static CookieOptions RequestTokenCookieOptions() =>
        new()
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Strict,
            // Every request is HTTPS, development included (ADR 016).
            Secure = true,
            Path = "/",
        };

    /// <summary>
    /// Refuses an unsafe request (anything but GET, HEAD, OPTIONS and TRACE) from a signed-in
    /// session without a valid token, with a 400 problem. Runs after authentication.
    /// </summary>
    public static IApplicationBuilder UseSessionAntiforgery(this IApplicationBuilder app) =>
        app.Use(
            async (context, next) =>
            {
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                    if (!await antiforgery.IsRequestValidAsync(context))
                    {
                        await TypedResults
                            .Problem(
                                statusCode: StatusCodes.Status400BadRequest,
                                title: "Missing or invalid antiforgery token",
                                detail: $"Send the {RequestTokenCookieName} cookie's value as the {HeaderName} header (GET /auth/antiforgery-token)."
                            )
                            .ExecuteAsync(context);
                        return;
                    }
                }

                await next(context);
            }
        );
}
