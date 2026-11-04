using System.Net.Http.Headers;
using System.Text.Json;
using AppTemplate.Web.Authentication;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// A web client's side of a session: the browser's cookie jar, where the HttpOnly refresh token
/// lives, and the access token the SPA keeps and sends as <c>Authorization: Bearer</c>. HTTPS,
/// because the cookie is Secure outside Development.
/// <para>
/// The test server isn't behind the /api proxy, but the cookie is scoped to the browser's path
/// (<c>/api/auth/refresh-token</c>). So the jar stores cookies against that path, and the refresh
/// and sign-out requests attach the cookie the way a browser would there.
/// </para>
/// </summary>
public sealed class TestSession(WebApplicationFactory<Program> factory)
{
    private static readonly Uri RefreshPath = new("https://localhost/api/auth/refresh-token");
    private readonly CookieContainer _cookies = new();

    public HttpClient Client { get; } =
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = false,
            }
        );

    public string? AccessToken { get; private set; }

    /// <summary>The raw <c>Set-Cookie</c> headers of the last response.</summary>
    public IReadOnlyList<string> LastSetCookies { get; private set; } = [];

    /// <summary>The refresh token cookie, as the browser holds it; <c>null</c> once deleted.</summary>
    public Cookie? RefreshCookie =>
        _cookies
            .GetCookies(RefreshPath)
            .FirstOrDefault(cookie => cookie.Name == RefreshTokenCookie.Name && !cookie.Expired);

    public Task<HttpResponseMessage> LoginAsync(
        string email,
        string password = CustomWebApplicationFactory<Program>.Password
    ) =>
        SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "/auth/login")
            {
                Content = JsonContent.Create(new { email, password }),
            },
            keepAccessToken: true
        );

    public Task<HttpResponseMessage> RegisterAsync(
        string email,
        string password = CustomWebApplicationFactory<Program>.Password,
        string name = "Ada Lovelace"
    ) =>
        SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "/auth/register")
            {
                Content = JsonContent.Create(
                    new
                    {
                        name,
                        email,
                        password,
                    }
                ),
            },
            keepAccessToken: false
        );

    /// <summary>POST /auth/change-password; keeps the new session's access token and cookie.</summary>
    public Task<HttpResponseMessage> ChangePasswordAsync(
        string currentPassword,
        string newPassword
    ) =>
        SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "/auth/change-password")
            {
                Content = JsonContent.Create(new { currentPassword, newPassword }),
            },
            keepAccessToken: true
        );

    /// <summary>POST /auth/refresh-token with <c>{}</c>, sending this jar's refresh cookie, or <paramref name="refreshToken"/>.</summary>
    public Task<HttpResponseMessage> RefreshAsync(string? refreshToken = null) =>
        SendAsync(
            WithRefreshCookie(
                new HttpRequestMessage(HttpMethod.Post, "/auth/refresh-token")
                {
                    Content = JsonContent.Create(new { }),
                },
                refreshToken
            ),
            keepAccessToken: true
        );

    /// <summary>DELETE /auth/refresh-token (sign out), with the access token and the refresh cookie.</summary>
    public Task<HttpResponseMessage> LogoutAsync() =>
        SendAsync(
            WithRefreshCookie(
                new HttpRequestMessage(HttpMethod.Delete, "/auth/refresh-token"),
                null
            ),
            keepAccessToken: false
        );

    /// <summary>Sends <paramref name="accessToken"/> from now on, as the SPA's interceptor does.</summary>
    public void Authenticate(string? accessToken) =>
        Client.DefaultRequestHeaders.Authorization = accessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", accessToken);

    private HttpRequestMessage WithRefreshCookie(HttpRequestMessage request, string? refreshToken)
    {
        var value = refreshToken ?? RefreshCookie?.Value;
        if (value is not null)
        {
            request.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={value}");
        }
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        bool keepAccessToken
    )
    {
        var response = await Client.SendAsync(request);

        LastSetCookies = response.Headers.TryGetValues("Set-Cookie", out var setCookies)
            ? [.. setCookies]
            : [];
        foreach (var header in LastSetCookies)
        {
            _cookies.SetCookies(RefreshPath, header);
        }

        if (keepAccessToken && response.IsSuccessStatusCode)
        {
            // As a string, so the test can still read the body itself.
            var json = await response.Content.ReadAsStringAsync();
            AccessToken = JsonSerializer
                .Deserialize<AccessTokenBody>(json, JsonSerializerOptions.Web)!
                .AccessToken;
            Authenticate(AccessToken);
        }

        return response;
    }

    /// <summary>What a token response body carries; userId and refreshToken never appear.</summary>
    public sealed record AccessTokenBody(string AccessToken, DateTime AccessTokenExpiry);
}
