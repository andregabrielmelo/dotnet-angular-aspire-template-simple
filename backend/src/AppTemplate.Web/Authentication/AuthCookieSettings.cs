namespace AppTemplate.Web.Authentication;

/// <summary>The browser-facing side of sessions (<c>Authentication</c>).</summary>
public sealed class AuthCookieSettings
{
    public const string SectionName = "Authentication";

    /// <summary>
    /// The origins the SPA is served from, such as <c>https://app.example.com</c>. Refresh and
    /// sign-out, which the refresh cookie authenticates, reject any other <c>Origin</c>.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>
    /// The path the refresh cookie is sent to, as the browser sees it: the SPA calls the API
    /// through <c>/api</c> (the dev server proxy, or the production host), so <c>/api/auth</c>.
    /// </summary>
    public string RefreshCookiePath { get; set; } = "/api/auth";

    public static bool IsOrigin(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && uri.PathAndQuery == "/"
        && !value.EndsWith('/');
}
