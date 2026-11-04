using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// Every route requires a signed-in user unless it is deliberately anonymous. Anonymous routes
/// are listed here, so making an endpoint public is a reviewed change to this test.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class EndpointAuthorization(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private static readonly string[] ExpectedAnonymousRoutes =
    [
        // (The health probes are anonymous too, but mapped in Development only.)
        // Signing in and up, and refreshing with the refresh cookie (no access token yet).
        "POST /auth/login",
        "POST /auth/refresh-token",
        "POST /auth/register",
    ];

    private static string Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["ANY"];
        return $"{string.Join(',', methods)} /{endpoint.RoutePattern.RawText?.TrimStart('/')}";
    }

    [Fact]
    public void ListsExactlyTheExpectedAnonymousRoutes()
    {
        var anonymous = factory
            .Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(Describe)
            .Order()
            .ToList();

        anonymous.ShouldBe(ExpectedAnonymousRoutes.Order());
    }

    [Fact]
    public async Task KeepsFastEndpointsTestUrlCachePrivate()
    {
        // FastEndpoints 8.3 maps this route without authorization metadata; it lists every route
        // and endpoint type name. The fallback policy is what makes it require a signed-in user.
        await factory.CreateClient().GetAndEnsureUnauthorizedAsync("/_test_url_cache_");
    }
}
