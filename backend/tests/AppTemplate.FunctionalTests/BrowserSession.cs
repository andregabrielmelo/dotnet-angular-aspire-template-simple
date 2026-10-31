using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AppTemplate.Web.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// The SPA's side of a session, over the real endpoints and the real JWT handler: a cookie jar
/// (refresh and antiforgery cookies), the app's <c>Origin</c>, the antiforgery header Angular
/// sends, and the access token kept in memory. HTTPS, because the cookies are Secure.
/// </summary>
public sealed class BrowserSession
{
    private readonly CookieContainer _cookies = new();
    private static readonly Uri BaseAddress = new("https://localhost");

    public BrowserSession(WebApplicationFactory<Program> factory, string? origin = null)
    {
        Client = factory.CreateDefaultClient(BaseAddress, new CookieContainerHandler(_cookies));
        Client.DefaultRequestHeaders.Add(
            "X-Forwarded-For",
            AppTemplateWebApplicationFactory.NewClientAddress()
        );
        Client.DefaultRequestHeaders.Add(
            "Origin",
            origin ?? AppTemplateWebApplicationFactory.AllowedOrigin
        );
    }

    public HttpClient Client { get; }

    /// <summary>The access token from the last sign-in, registration or refresh.</summary>
    public string? AccessToken { get; private set; }

    /// <summary>The <c>expiresIn</c> (seconds) that came with <see cref="AccessToken"/>.</summary>
    public int? ExpiresIn { get; private set; }

    /// <summary>The refresh token cookie's value, as the browser holds it.</summary>
    public string? RefreshToken
    {
        get => Cookie(AuthCookies.RefreshCookieName)?.Value;
        set =>
            _cookies.Add(
                new Cookie(
                    AuthCookies.RefreshCookieName,
                    value,
                    AppTemplateWebApplicationFactory.RefreshCookiePath,
                    BaseAddress.Host
                )
                {
                    Secure = true,
                    HttpOnly = true,
                }
            );
    }

    public Cookie? Cookie(string name) =>
        _cookies.GetAllCookies().FirstOrDefault(cookie => cookie.Name == name);

    public Task<HttpResponseMessage> RegisterAsync(
        string email,
        string password = AppTemplateWebApplicationFactory.DefaultPassword,
        string name = "Ada Lovelace"
    ) =>
        KeepAccessToken(
            Client.PostAsJsonAsync(
                "/auth/register",
                new
                {
                    name,
                    email,
                    password,
                }
            )
        );

    public Task<HttpResponseMessage> LoginAsync(
        string email,
        string password = AppTemplateWebApplicationFactory.DefaultPassword
    ) => KeepAccessToken(Client.PostAsJsonAsync("/auth/login", new { email, password }));

    public async Task<HttpResponseMessage> RefreshAsync()
    {
        await EnsureAntiforgeryAsync();
        return await KeepAccessToken(Client.SendAsync(WithAntiforgery("/auth/refresh")));
    }

    public async Task<HttpResponseMessage> LogoutAsync()
    {
        await EnsureAntiforgeryAsync();
        var response = await Client.SendAsync(WithAntiforgery("/auth/logout"));
        AccessToken = null;
        return response;
    }

    /// <summary>A request with the access token, as the SPA's interceptor sends it.</summary>
    public Task<HttpResponseMessage> GetWithAccessTokenAsync(
        string path,
        string? accessToken = null
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken ?? AccessToken
        );
        return Client.SendAsync(request);
    }

    public async Task EnsureAntiforgeryAsync()
    {
        if (Cookie(AuthCookies.AntiforgeryRequestCookieName) is null)
        {
            (await Client.GetAsync("/auth/antiforgery")).EnsureSuccessStatusCode();
        }
    }

    public HttpRequestMessage WithAntiforgery(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (Cookie(AuthCookies.AntiforgeryRequestCookieName) is { } token)
        {
            request.Headers.Add(AuthCookies.AntiforgeryHeaderName, token.Value);
        }
        return request;
    }

    private async Task<HttpResponseMessage> KeepAccessToken(Task<HttpResponseMessage> sending)
    {
        var response = await sending;
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
            AccessToken = body!.AccessToken;
            ExpiresIn = body.ExpiresIn;
        }
        return response;
    }
}
