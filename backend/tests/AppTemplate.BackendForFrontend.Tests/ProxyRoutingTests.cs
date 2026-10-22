using System.Net;
using System.Security.Claims;
using AppTemplate.BackendForFrontend.Configurations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace AppTemplate.BackendForFrontend.Tests;

/// <summary>
/// What the browser calls (<c>/api/...</c>) is what reaches the Web API (<c>/...</c>):
/// YARP strips <c>/api</c>, adds the session's access token on the authenticated route, and
/// never forwards the cookie. The Web API is replaced by a handler that records each request.
/// </summary>
public class ProxyRoutingTests : IClassFixture<ProxyRoutingTests.ProxyFactory>
{
    public const string AccessToken = "test-access-token";
    private readonly ProxyFactory _factory;

    public ProxyRoutingTests(ProxyFactory factory)
    {
        _factory = factory;
        _factory.Api.Requests.Clear();
    }

    private static HttpRequestMessage ApiRequest(HttpMethod method, string path) =>
        new(method, path)
        {
            Headers = { { AntiforgeryHeaderMiddleware.HeaderName, "1" } },
            Content = method == HttpMethod.Post ? new StringContent("{}") : null,
        };

    [Fact]
    public async Task SignedInCall_ReachesTheApiRouteWithTheAccessToken()
    {
        var client = _factory.CreateBrowserClient();
        await client.PostAsync(ProxyFactory.SignInPath, null);

        var response = await client.SendAsync(ApiRequest(HttpMethod.Get, "/api/users/me"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var forwarded = Assert.Single(_factory.Api.Requests);
        Assert.Equal("/users/me", forwarded.Path);
        Assert.Equal($"Bearer {AccessToken}", forwarded.Authorization);
        Assert.False(forwarded.HadCookie);
    }

    [Fact]
    public async Task SignedOutApiCall_IsRejectedWithoutReachingTheApi()
    {
        var response = await _factory
            .CreateBrowserClient()
            .SendAsync(ApiRequest(HttpMethod.Get, "/api/users/me"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.Api.Requests);
    }

    public sealed record ForwardedRequest(string Path, string? Authorization, bool HadCookie);

    /// <summary>Stands in for the Web API: records each forwarded request and answers 200.</summary>
    public sealed class RecordingApi : HttpMessageHandler
    {
        public List<ForwardedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            lock (Requests)
            {
                Requests.Add(
                    new ForwardedRequest(
                        request.RequestUri!.AbsolutePath,
                        request.Headers.Authorization?.ToString(),
                        request.Headers.Contains("Cookie")
                    )
                );
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    public class ProxyFactory : BackendForFrontendFactory
    {
        public const string SignInPath = "/test/sign-in";

        public RecordingApi Api { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            // Service discovery resolves the "web" cluster; the handler below answers for it.
            builder.UseSetting("services:web:https:0", "https://web.test");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IForwarderHttpClientFactory>(new StubForwarderFactory(Api));
                services.AddSingleton<IStartupFilter, SignInStartupFilter>();
            });
        }

        private sealed class StubForwarderFactory(HttpMessageHandler handler)
            : IForwarderHttpClientFactory
        {
            public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) =>
                new(handler, disposeHandler: false);
        }

        /// <summary>
        /// Signs the browser in the way the OpenID Connect callback would: a session cookie whose
        /// properties hold the tokens, which Duende.AccessTokenManagement reads back.
        /// </summary>
        private sealed class SignInStartupFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
                app =>
                {
                    // Not app.Map: that would set PathBase and scope the cookie to this path.
                    app.Use(
                        async (context, nextMiddleware) =>
                        {
                            if (context.Request.Path != SignInPath)
                            {
                                await nextMiddleware(context);
                                return;
                            }

                            var identity = new ClaimsIdentity(
                                [new Claim("sub", "user-1"), new Claim("name", "Ada")],
                                CookieAuthenticationDefaults.AuthenticationScheme
                            );
                            var properties = new AuthenticationProperties();
                            properties.StoreTokens([
                                new AuthenticationToken
                                {
                                    Name = "access_token",
                                    Value = AccessToken,
                                },
                                new AuthenticationToken
                                {
                                    Name = "refresh_token",
                                    Value = "test-refresh-token",
                                },
                                new AuthenticationToken { Name = "token_type", Value = "Bearer" },
                                new AuthenticationToken
                                {
                                    Name = "expires_at",
                                    Value = DateTimeOffset.UtcNow.AddHours(1).ToString("o"),
                                },
                            ]);
                            await context.SignInAsync(
                                CookieAuthenticationDefaults.AuthenticationScheme,
                                new ClaimsPrincipal(identity),
                                properties
                            );
                        }
                    );
                    next(app);
                };
        }
    }
}
