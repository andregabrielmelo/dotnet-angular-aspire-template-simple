using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// A browser's side of a session: a cookie jar on <c>https://localhost</c> (HTTPS, because the
/// session cookie is Secure outside Development) that every request through <see cref="Client"/>
/// sends and updates, as a browser does. Signing in sets Identity's session cookie; nothing else
/// is needed to be signed in.
/// </summary>
public sealed class TestSession
{
    private static readonly Uri Origin = new("https://localhost/");

    /// <summary>Identity's application cookie, with its default name: the session.</summary>
    public static readonly string SessionCookieName =
        CookieAuthenticationDefaults.CookiePrefix + IdentityConstants.ApplicationScheme;
    private readonly CookieContainer _cookies = new();

    public TestSession(WebApplicationFactory<Program> factory) =>
        Client = factory.CreateDefaultClient(Origin, new CookieJar(this));

    public HttpClient Client { get; }

    /// <summary>The raw <c>Set-Cookie</c> headers of the last response.</summary>
    public IReadOnlyList<string> LastSetCookies { get; private set; } = [];

    /// <summary>The session cookie, as the browser holds it; <c>null</c> once deleted.</summary>
    public Cookie? SessionCookie =>
        _cookies
            .GetCookies(Origin)
            .FirstOrDefault(cookie => cookie.Name == SessionCookieName && !cookie.Expired);

    /// <summary>Replaces the session cookie's value, as a client tampering with it would.</summary>
    public void SetSessionCookie(string value) =>
        _cookies.Add(Origin, new Cookie(SessionCookieName, value) { Secure = true });

    public Task<HttpResponseMessage> LoginAsync(
        string email,
        string password = CustomWebApplicationFactory<Program>.Password
    ) => Client.PostAsJsonAsync("/auth/login", new { email, password });

    public Task<HttpResponseMessage> RegisterAsync(
        string email,
        string password = CustomWebApplicationFactory<Program>.Password,
        string name = "Ada Lovelace"
    ) =>
        Client.PostAsJsonAsync(
            "/auth/register",
            new
            {
                name,
                email,
                password,
            }
        );

    public Task<HttpResponseMessage> ChangePasswordAsync(
        string currentPassword,
        string newPassword
    ) => Client.PostAsJsonAsync("/auth/change-password", new { currentPassword, newPassword });

    public Task<HttpResponseMessage> LogoutAsync() => Client.PostAsync("/auth/logout", null);

    /// <summary>Sends the jar's cookies with each request and keeps what the response sets.</summary>
    private sealed class CookieJar(TestSession session) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var cookies = session._cookies.GetCookieHeader(request.RequestUri!);
            if (cookies.Length > 0)
            {
                request.Headers.Add("Cookie", cookies);
            }

            var response = await base.SendAsync(request, cancellationToken);

            session.LastSetCookies = response.Headers.TryGetValues("Set-Cookie", out var headers)
                ? [.. headers]
                : [];
            foreach (var header in session.LastSetCookies)
            {
                session._cookies.SetCookies(request.RequestUri!, header);
            }

            return response;
        }
    }
}
