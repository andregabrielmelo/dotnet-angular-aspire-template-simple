using AppTemplate.UseCases.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

namespace AppTemplate.Web.Authentication;

/// <summary>The access token handed to the SPA, which keeps it in memory only.</summary>
/// <param name="ExpiresIn">Seconds until the access token expires.</param>
public sealed record AccessTokenResponse(string AccessToken, int ExpiresIn);

/// <summary>
/// Everything browser-specific about sessions, in one place. The refresh token lives in an
/// HttpOnly, SameSite=Strict cookie that only reaches <see cref="AuthCookieSettings.RefreshCookiePath"/>.
/// Because that cookie authenticates refresh and sign-out, those endpoints also require an
/// allowed <c>Origin</c> and a valid antiforgery token, rather than relying on SameSite alone
/// (a sibling subdomain is the same site). See ADR 007.
/// </summary>
public sealed class AuthCookies(
    IOptions<AuthCookieSettings> settings,
    IAntiforgery antiforgery,
    IHostEnvironment environment,
    TimeProvider timeProvider
)
{
    public const string RefreshCookieName = "apptemplate_refresh";

    /// <summary>Readable by the SPA; Angular's HttpClient echoes it back in <see cref="AntiforgeryHeaderName"/>.</summary>
    public const string AntiforgeryRequestCookieName = "XSRF-TOKEN";
    public const string AntiforgeryHeaderName = "X-XSRF-TOKEN";

    // The Angular dev server is plain HTTP; everywhere else the cookies are HTTPS-only.
    private bool Secure => !environment.IsDevelopment();

    /// <summary>Stores the session's refresh token and returns what the SPA gets in the body.</summary>
    public AccessTokenResponse SignIn(HttpContext context, AuthSession session)
    {
        context.Response.Cookies.Append(
            RefreshCookieName,
            session.RefreshToken,
            RefreshCookieOptions(session.RefreshTokenExpiresAt)
        );
        IssueAntiforgery(context);

        var expiresIn = session.AccessTokenExpiresAt - timeProvider.GetUtcNow();
        return new AccessTokenResponse(
            session.AccessToken,
            (int)Math.Max(0, Math.Floor(expiresIn.TotalSeconds))
        );
    }

    public string? ReadRefreshToken(HttpContext context) =>
        context.Request.Cookies.TryGetValue(RefreshCookieName, out var token)
        && !string.IsNullOrEmpty(token)
            ? token
            : null;

    public void DeleteRefreshToken(HttpContext context) =>
        context.Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions(expires: null));

    /// <summary>Issues the antiforgery cookie pair the SPA needs before refreshing or signing out.</summary>
    public void IssueAntiforgery(HttpContext context)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append(
            AntiforgeryRequestCookieName,
            tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,
                Secure = Secure,
                SameSite = SameSiteMode.Strict,
                Path = "/",
            }
        );
    }

    /// <summary>
    /// <c>null</c> when the request comes from an allowed origin with a valid antiforgery
    /// token; otherwise the 403 to return.
    /// </summary>
    public async Task<ProblemHttpResult?> RejectCrossSiteRequestAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        var allowedOrigin = settings.Value.AllowedOrigins.Contains(
            origin,
            StringComparer.OrdinalIgnoreCase
        );

        if (!allowedOrigin || !await antiforgery.IsRequestValidAsync(context))
        {
            return TypedResults.Problem(
                detail: "The request must come from the application, with its antiforgery token.",
                statusCode: StatusCodes.Status403Forbidden
            );
        }

        return null;
    }

    private CookieOptions RefreshCookieOptions(DateTimeOffset? expires) =>
        new()
        {
            HttpOnly = true,
            Secure = Secure,
            SameSite = SameSiteMode.Strict,
            Path = settings.Value.RefreshCookiePath,
            Expires = expires,
            IsEssential = true,
        };
}
