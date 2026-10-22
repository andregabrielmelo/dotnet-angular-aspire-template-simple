using System.Diagnostics.Metrics;
using AppTemplate.Core.Interfaces;
using AppTemplate.Infrastructure.Data;
using AppTemplate.UseCases.Telemetry;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Xunit;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// Hosts the real API - HTTP -> FastEndpoints -> Mediator -> EF Core -> Postgres - against a
/// fresh database in a shared Postgres container (see <see cref="PostgresTestDatabase"/>), so
/// unique indexes, database defaults, raw SQL and owned types behave as in production. Needs
/// Docker: mark every test class that uses this factory with
/// <c>[Trait(TestCategories.Name, TestCategories.RequiresDocker)]</c>.
/// Keycloak JWT validation is replaced by <see cref="TestAuthHandler"/> - use
/// <see cref="CreateAuthenticatedClient"/> for calls that need a signed-in user.
/// </summary>
public class AppTemplateWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>The app's Postgres connection string, set by <see cref="InitializeAsync"/>.</summary>
    protected string? ConnectionString { get; set; }

    /// <summary>Replaces the Keycloak Admin API client; inspect or reconfigure it per test.</summary>
    public FakePasswordResetService PasswordResetService { get; } = new();

    /// <summary>Records emails instead of sending them over SMTP.</summary>
    public FakeEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting(
            "ConnectionStrings:apptemplate",
            ConnectionString
                ?? throw new InvalidOperationException(
                    "The factory is used before InitializeAsync - use it as an xUnit fixture."
                )
        );
        // KeycloakAdminOptions is validated on start; the real client is replaced below.
        builder.UseSetting("Keycloak:Admin:ClientSecret", "functional-tests");
        builder.UseSetting("Keycloak:Authority", "https://keycloak.test/realms/apptemplate");
        // Every request arrives from a simulated backend for frontend on loopback (see
        // TestPeerAddress), the one trusted proxy, so each client's X-Forwarded-For is honored.
        builder.UseSetting("ForwardedHeaders:KnownProxies:0", TestPeerAddress.TrustedProxy);
        // Far above anything a test sends; RateLimitingTests lowers them in its own host.
        builder.UseSetting("RateLimiting:AnonymousPermitLimit", "100000");
        builder.UseSetting("RateLimiting:AuthenticatedPermitLimit", "100000");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestPeerAddress>();

            services.RemoveAll<IPasswordResetService>();
            services.AddSingleton<IPasswordResetService>(PasswordResetService);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { }
                );
        });
    }

    /// <summary>Creates this host's database (by running the migrations) before any test uses it.</summary>
    public virtual async Task InitializeAsync()
    {
        ConnectionString = await PostgresTestDatabase.NewDatabaseConnectionStringAsync();

        using var scope = Services.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
            .Database.MigrateAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    private static int _nextClientAddress;

    /// <summary>Records one of this host's <see cref="ApplicationMetrics"/> instruments from now on.</summary>
    public MetricCollector<T> CollectMetric<T>(string instrument)
        where T : struct =>
        new(Services.GetRequiredService<IMeterFactory>(), ApplicationMetrics.MeterName, instrument);

    /// <summary>
    /// A client whose requests are authenticated as the given <c>sub</c>, holding the given
    /// API permissions (Keycloak client roles).
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string subject, params string[] permissions)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, subject);
        // Anonymous rate limits key on the client address (as forwarded by the trusted
        // backend for frontend) - every client gets its own, so tests never share a window.
        var n = Interlocked.Increment(ref _nextClientAddress);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.1.{n / 250}.{n % 250 + 1}");
        if (permissions.Length > 0)
        {
            client.DefaultRequestHeaders.Add(
                TestAuthHandler.PermissionsHeader,
                string.Join(',', permissions)
            );
        }
        return client;
    }
}
