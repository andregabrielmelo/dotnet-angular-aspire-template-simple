using System.Net;
using AppTemplate.BackendForFrontend.Configurations;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace AppTemplate.BackendForFrontend.Tests;

/// <summary>
/// The backend for frontend logs one line per request, with the signed-in caller, through the
/// same redacting pipeline as the API, and never its session cookie or the access token it
/// attaches to proxied calls.
/// </summary>
public class RequestLoggingTests : IClassFixture<RequestLoggingTests.CapturingProxyFactory>
{
    private readonly CapturingProxyFactory _factory;

    public RequestLoggingTests(CapturingProxyFactory factory) => _factory = factory;

    [Fact]
    public async Task ProxiedCall_IsLoggedWithoutCookieOrToken()
    {
        var client = _factory.CreateBrowserClient();
        var signIn = await client.PostAsync(ProxyRoutingTests.ProxyFactory.SignInPath, null);
        var sessionCookie = signIn
            .Headers.GetValues("Set-Cookie")
            .Select(cookie => cookie.Split(';')[0].Split('=', 2)[1])
            .First(value => value.Length > 20);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me?probe=1");
        request.Headers.Add(AntiforgeryHeaderMiddleware.HeaderName, "1");

        var response = await client.SendAsync(request);
        await _factory.Logs.WaitForAsync("HTTP GET /api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requestLine = _factory
            .Logs.Console.Split('\n')
            .Single(line => line.Contains("HTTP GET /api/users/me responded 200"));
        Assert.Contains("\"Subject\":\"user-1\"", requestLine);
        Assert.DoesNotContain("probe=1", requestLine); // never the query string
        foreach (var secret in new[] { sessionCookie, ProxyRoutingTests.AccessToken })
        {
            Assert.DoesNotContain(secret, _factory.Logs.Console);
            Assert.DoesNotContain(secret, _factory.Logs.Otlp);
        }
    }

    public sealed class CapturingProxyFactory : ProxyRoutingTests.ProxyFactory
    {
        public LogCapture Logs { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(Logs.Configure);
        }
    }
}
