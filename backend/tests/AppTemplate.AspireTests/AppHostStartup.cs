using System.Net;
using AppTemplate.Tests;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Shouldly;
using Xunit;

namespace AppTemplate.AspireTests;

/// <summary>
/// Smoke test of the whole local composition (ADR 002, ADR 006): starts the real AppHost (Postgres,
/// Redis, Mailpit, the API and the Angular app) and waits for the API to be healthy. It catches
/// what the other test projects can't: broken resource references, settings the AppHost passes on,
/// service discovery, and health checks. Needs Docker, like the integration tests.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class AppHostStartup
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task StartsTheApiHealthy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppTemplate_AppHost>(
                cancellationToken
            );
        UseThrowawayContainers(builder);
        await using var app = await builder.BuildAsync(cancellationToken);

        await app.StartAsync(cancellationToken);
        await app
            .ResourceNotifications.WaitForResourceHealthyAsync("web", cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken);

        using var client = app.CreateHttpClient("web", "https");
        (await client.GetAsync("/alive", cancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/health", cancellationToken)).StatusCode.ShouldBe(
            HttpStatusCode.OK
        );
    }

    /// <summary>
    /// The AppHost keeps Postgres and Redis in persistent containers with data volumes, for local
    /// development. A test run must neither reuse nor change a developer's data, so here every
    /// container lives only as long as the test and keeps no volume.
    /// </summary>
    private static void UseThrowawayContainers(IDistributedApplicationTestingBuilder builder)
    {
        foreach (var container in builder.Resources.OfType<ContainerResource>().ToList())
        {
            builder.CreateResourceBuilder(container).WithLifetime(ContainerLifetime.Session);

            foreach (
                var volume in container
                    .Annotations.OfType<ContainerMountAnnotation>()
                    .Where(mount => mount.Type == ContainerMountType.Volume)
                    .ToList()
            )
            {
                container.Annotations.Remove(volume);
            }
        }
    }
}
