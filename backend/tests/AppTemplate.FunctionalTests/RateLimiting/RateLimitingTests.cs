using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppTemplate.Web.Configurations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppTemplate.FunctionalTests.RateLimiting;

/// <summary>
/// The global limiter partitions by validated <c>sub</c> or by client address, never by
/// anything the client controls, and rejects with a 429 problem details response carrying
/// <c>Retry-After</c>. The named per-endpoint policies key on the same partition.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class RateLimitingTests(RateLimitingTests.LowLimitFactory factory)
    : IClassFixture<RateLimitingTests.LowLimitFactory>
{
    private const int Limit = 3;
    private static int _nextAddress;

    private static string NewAddress()
    {
        var n = Interlocked.Increment(ref _nextAddress);
        return $"10.9.{n / 250}.{n % 250 + 1}";
    }

    /// <summary>An anonymous client behind the trusted proxy, forwarded as <paramref name="address"/>.</summary>
    private HttpClient AnonymousClient(string address)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", address);
        return client;
    }

    private static async Task<HttpResponseMessage> ExhaustAsync(
        Func<Task<HttpResponseMessage>> send,
        int requests = Limit
    )
    {
        for (var i = 0; i < requests; i++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await send()).StatusCode);
        }
        return await send();
    }

    [Fact]
    public async Task OverTheLimit_IsA429ProblemWithRetryAfter()
    {
        var client = AnonymousClient(NewAddress());

        var rejected = await ExhaustAsync(() => client.GetAsync("/users/me"));

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        var retryAfter = rejected.Headers.RetryAfter?.Delta;
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter.Value, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
        var body = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(429, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        // The security headers middleware runs before the limiter, so rejections carry them too.
        Assert.Equal(["nosniff"], rejected.Headers.GetValues("X-Content-Type-Options"));
    }

    [Fact]
    public async Task AnonymousFlood_DoesNotAffectASignedInUserFromTheSameAddress()
    {
        var address = NewAddress();
        var anonymous = AnonymousClient(address);
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await ExhaustAsync(() => anonymous.GetAsync("/users/me"))).StatusCode
        );

        var user = factory.CreateClient();
        user.DefaultRequestHeaders.Add("X-Forwarded-For", address);
        user.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, $"sub-{Guid.NewGuid():N}");
        var response = await user.GetAsync("/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SignedInUsers_HaveSeparatePartitions()
    {
        var first = factory.CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}");
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await ExhaustAsync(() => first.GetAsync("/users/me"))).StatusCode
        );

        var second = factory.CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/users/me")).StatusCode);
    }

    [Fact]
    public async Task SpoofedForwardedFor_FromAnUntrustedPeer_DoesNotChangeThePartition()
    {
        var peer = "203.0.113.7"; // connects directly, not through the trusted proxy
        var n = 0;
        Task<HttpResponseMessage> SendWithNewSpoofedAddress()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/users/me");
            request.Headers.Add(TestPeerAddress.HeaderName, peer);
            request.Headers.Add("X-Forwarded-For", $"198.51.100.{++n}");
            return factory.CreateClient().SendAsync(request);
        }

        var rejected = await ExhaustAsync(SendWithNewSpoofedAddress);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Theory]
    [InlineData("/health", true)]
    [InlineData("/alive", true)]
    [InlineData("/users", false)]
    public void HealthEndpoints_AreExcluded(string path, bool excluded) =>
        Assert.Equal(excluded, ClientPartition.IsExcluded(path));

    public sealed class LowLimitFactory : AppTemplateWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("RateLimiting:AnonymousPermitLimit", Limit.ToString());
            builder.UseSetting("RateLimiting:AuthenticatedPermitLimit", Limit.ToString());
        }
    }
}
