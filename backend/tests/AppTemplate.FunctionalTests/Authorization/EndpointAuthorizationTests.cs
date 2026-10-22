using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppTemplate.FunctionalTests.Authorization;

/// <summary>
/// Every route requires a signed-in user unless it is deliberately anonymous (ADR 010).
/// Anonymous routes are listed here, so making an endpoint public is a reviewed change to this
/// test. Routes that declare nothing fall back to requiring authentication.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class EndpointAuthorizationTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static readonly string[] ExpectedAnonymousRoutes =
    [
        // Health probes (ServiceDefaults.MapDefaultEndpoints): orchestrators call them without
        // credentials, and they answer with only a status word.
        "ANY /alive",
        "ANY /health",
        "ANY /health/dependencies",
    ];

    private IEnumerable<RouteEndpoint> RouteEndpoints() =>
        factory
            .Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>();

    private static string Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["ANY"];
        return $"{string.Join(',', methods)} /{endpoint.RoutePattern.RawText?.TrimStart('/')}";
    }

    [Fact]
    public void AnonymousRoutes_AreExactlyTheExpectedOnes()
    {
        var anonymous = RouteEndpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(Describe)
            .Order()
            .ToList();

        Assert.Equal(ExpectedAnonymousRoutes.Order(), anonymous);
    }

    [Fact]
    public async Task RoutesWithoutAuthorizationMetadata_FallBackToRequiringAuthentication()
    {
        var fallback = await factory
            .Services.GetRequiredService<IAuthorizationPolicyProvider>()
            .GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(
            fallback.Requirements,
            requirement =>
                requirement
                is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement
        );
    }

    [Fact]
    public async Task FastEndpointsTestUrlCache_IsNotPublic()
    {
        // FastEndpoints 8.2/8.3 always maps this route, without authorization metadata. It lists
        // every route and endpoint type name, so anonymous callers must not reach it.
        var response = await factory.CreateClient().GetAsync("/_test_url_cache_");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
