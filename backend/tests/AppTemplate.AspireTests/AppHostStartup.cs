using System.Net;
using AppTemplate.Tests;
using Aspire.Hosting.Testing;
using Shouldly;
using Xunit;

namespace AppTemplate.AspireTests;

/// <summary>
/// Smoke test of the whole local composition (ADR 002, ADR 006): the real AppHost (Postgres,
/// Redis, Mailpit, the API and the Angular app, started by <see cref="AppHostFixture"/>) comes up
/// with the API and the Angular app healthy. It catches what the other test projects can't:
/// broken resource references, settings the AppHost passes on, service discovery, and health
/// checks. Needs Docker, like the integration tests.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AppHostStartup(AppHostFixture fixture)
{
    [Fact]
    public async Task StartsTheApiHealthy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var client = fixture.App.CreateHttpClient("web", "https");
        (await client.GetAsync("/alive", cancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/health", cancellationToken)).StatusCode.ShouldBe(
            HttpStatusCode.OK
        );
    }
}
