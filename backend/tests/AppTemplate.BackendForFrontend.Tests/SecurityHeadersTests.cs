using AppTemplate.BackendForFrontend.Configurations;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace AppTemplate.BackendForFrontend.Tests;

/// <summary>
/// The SPA document carries the CSP and anti-framing headers; every response, HTML or not,
/// carries <c>nosniff</c> and a referrer policy. Outside Development the SPA is served from
/// <c>wwwroot</c>, so the factory points the web root at a folder with a stand-in index.html.
/// </summary>
public class SecurityHeadersTests : IClassFixture<SecurityHeadersTests.SpaFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersTests(SpaFactory factory) => _client = factory.CreateBrowserClient();

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/some/client-side/route")]
    public async Task SpaDocument_HasTheBrowserSecurityHeaders(string path)
    {
        var response = await _client.GetAsync(path);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            SecurityHeadersMiddleware.ContentSecurityPolicy,
            Header(response, "Content-Security-Policy")
        );
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Header(response, "Referrer-Policy"));
    }

    [Fact]
    public void ContentSecurityPolicy_AllowsNoInlineOrEvaluatedScriptAndNoFraming()
    {
        var directives = SecurityHeadersMiddleware
            .ContentSecurityPolicy.Split(';', StringSplitOptions.TrimEntries)
            .ToDictionary(d => d.Split(' ')[0], d => d);

        Assert.Equal("script-src 'self'", directives["script-src"]);
        Assert.Equal("frame-ancestors 'none'", directives["frame-ancestors"]);
        Assert.Equal("object-src 'none'", directives["object-src"]);
        Assert.DoesNotContain("unsafe-eval", SecurityHeadersMiddleware.ContentSecurityPolicy);
    }

    [Fact]
    public async Task JsonResponse_HasNosniffButNoDocumentPolicy()
    {
        // A signed-out call to the user endpoint, CSRF header included: a JSON problem response.
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BackendForFrontendEndpoints.UserPath
        );
        request.Headers.Add(AntiforgeryHeaderMiddleware.HeaderName, "1");
        var response = await _client.SendAsync(request);

        Assert.Contains("json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Header(response, "Referrer-Policy"));
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task ErrorResponse_HasNosniff()
    {
        // A signed-out call to the user endpoint without the CSRF header: a 401 problem.
        var response = await _client.GetAsync(BackendForFrontendEndpoints.UserPath);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    public sealed class SpaFactory : BackendForFrontendFactory, IDisposable
    {
        private readonly string _webRoot = Directory.CreateTempSubdirectory("bff-wwwroot").FullName;

        public SpaFactory() =>
            File.WriteAllText(
                Path.Combine(_webRoot, "index.html"),
                "<!doctype html><html><body><app-root></app-root></body></html>"
            );

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseWebRoot(_webRoot);
        }

        void IDisposable.Dispose()
        {
            Dispose();
            Directory.Delete(_webRoot, recursive: true);
        }
    }
}
