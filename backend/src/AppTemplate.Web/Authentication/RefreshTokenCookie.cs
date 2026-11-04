using AppTemplate.Web.Configurations;
using Microsoft.Extensions.Options;

namespace AppTemplate.Web.Authentication;

/// <summary>
/// The refresh token's cookie, for web clients: HttpOnly, so page script can't read it, and sent
/// only to <see cref="JwtSettings.RefreshCookiePath"/> (refresh and logout). <c>SameSite=Strict</c>
/// keeps it off cross-site requests, so it needs no CSRF token of its own (ADR 012).
/// </summary>
public sealed class RefreshTokenCookie(IOptions<JwtSettings> settings, IHostEnvironment environment)
{
    public const string Name = "apptemplate_refresh";

    public void Append(HttpContext context, TokenResponse tokens) =>
        context.Response.Cookies.Append(
            Name,
            tokens.RefreshToken,
            Options(new DateTimeOffset(tokens.RefreshExpiry, TimeSpan.Zero))
        );

    public string? Read(HttpContext context) =>
        context.Request.Cookies.TryGetValue(Name, out var token) && !string.IsNullOrEmpty(token)
            ? token
            : null;

    public void Delete(HttpContext context) =>
        context.Response.Cookies.Delete(Name, Options(expires: null));

    private CookieOptions Options(DateTimeOffset? expires) =>
        new()
        {
            HttpOnly = true,
            // The Angular dev server is plain HTTP; everywhere else the cookie is HTTPS-only.
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = settings.Value.RefreshCookiePath,
            Expires = expires,
            IsEssential = true,
        };
}
