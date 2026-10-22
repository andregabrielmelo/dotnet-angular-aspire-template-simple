using System.Net;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace AppTemplate.FunctionalTests.Health;

/// <summary>
/// The two probes exist in a non-Development host (these tests run as "Testing"), and
/// readiness follows Postgres while liveness doesn't.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class HealthEndpointTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    [Theory]
    [InlineData("/alive")]
    [InlineData("/health")]
    public async Task NonDevelopmentHost_ExposesEveryProbeAnonymously(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Only the status word: nothing about which dependency is where.
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}

/// <summary>A host whose Postgres is unreachable: it must report not ready, but alive.</summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class PostgresOutageHealthTests(PostgresOutageHealthTests.NoDatabaseFactory factory)
    : IClassFixture<PostgresOutageHealthTests.NoDatabaseFactory>
{
    [Fact]
    public async Task WithPostgresDown_HealthIs503AndAliveIs200()
    {
        var client = factory.CreateClient();

        var health = await client.GetAsync("/health");
        var alive = await client.GetAsync("/alive");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
        Assert.Equal("Unhealthy", await health.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
    }

    public sealed class NoDatabaseFactory : AppTemplateWebApplicationFactory
    {
        // Nothing listens on port 1: connections are refused immediately.
        public override Task InitializeAsync()
        {
            ConnectionString =
                "Host=127.0.0.1;Port=1;Database=unreachable;Username=none;Password=none;Timeout=2";
            return Task.CompletedTask;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            base.ConfigureWebHost(builder);
    }
}
