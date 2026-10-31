using System.Security.Cryptography;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Interfaces;
using AppTemplate.Core.ValueObjects;
using AppTemplate.Infrastructure.Data;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// Hosts the real API - HTTP -> FastEndpoints -> Mediator -> EF Core -> Postgres - against a
/// fresh database in a shared Postgres container (see <see cref="PostgresTestDatabase"/>), so
/// unique indexes, database defaults, raw SQL and owned types behave as in production. Needs
/// Docker: mark every test class that uses this factory with
/// <c>[Trait(TestCategories.Name, TestCategories.RequiresDocker)]</c>.
/// <para>
/// A request with <see cref="TestAuthHandler.UserHeader"/> is signed in as that user (see
/// <see cref="CreateAuthenticatedClient"/>); any other request goes through the real JWT bearer
/// validation, so tokens from <c>/auth/login</c> work too (see <see cref="BrowserSession"/>).
/// </para>
/// </summary>
public class AppTemplateWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>The origin <see cref="BrowserSession"/> sends, the only one refresh and sign-out accept.</summary>
    public const string AllowedOrigin = "https://app.test";

    /// <summary>The test server has no /api proxy in front, so the refresh cookie goes to /auth.</summary>
    public const string RefreshCookiePath = "/auth";

    /// <summary>The password of users made by <see cref="CreateUserAsync"/>.</summary>
    public const string DefaultPassword = "correct horse battery staple";

    /// <summary>Random per run, so no test depends on a known key.</summary>
    public static readonly string SigningKey = Convert.ToBase64String(
        RandomNumberGenerator.GetBytes(48)
    );

    /// <summary>The app's Postgres connection string, set by <see cref="InitializeAsync"/>.</summary>
    protected string? ConnectionString { get; set; }

    /// <summary>Records emails instead of sending them over SMTP.</summary>
    public FakeEmailSender EmailSender { get; } = new();

    /// <summary>
    /// The app's clock for sessions (refresh token lifetimes and rotation). Starts at the real
    /// time and only moves when a test advances it. Access token validation keeps the real
    /// clock, so don't use an access token issued after advancing it far.
    /// </summary>
    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

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
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Authentication:AllowedOrigins:0", AllowedOrigin);
        builder.UseSetting("Authentication:RefreshCookiePath", RefreshCookiePath);
        builder.UseSetting("Mailserver:FromAddress", "noreply@app.test");
        // Every request arrives from a simulated reverse proxy on loopback (see TestPeerAddress),
        // the one trusted proxy, so each client's X-Forwarded-For is honored.
        builder.UseSetting("ForwardedHeaders:KnownProxies:0", TestPeerAddress.TrustedProxy);
        // Far above anything a test sends; RateLimitingTests lowers them in its own host.
        builder.UseSetting("RateLimiting:AnonymousPermitLimit", "100000");
        builder.UseSetting("RateLimiting:AuthenticatedPermitLimit", "100000");
        builder.UseSetting("RateLimiting:AuthenticationPermitLimit", "100000");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestPeerAddress>();

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    options =>
                        // Without the test header, authenticate with a real access token.
                        options.ForwardDefaultSelector = context =>
                            context.Request.Headers.ContainsKey(TestAuthHandler.UserHeader)
                                ? null
                                : JwtBearerDefaults.AuthenticationScheme
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

    /// <summary>A distinct client address, so tests never share a rate limit window.</summary>
    public static string NewClientAddress()
    {
        var n = Interlocked.Increment(ref _nextClientAddress);
        return $"10.1.{n / 250}.{n % 250 + 1}";
    }

    /// <summary>
    /// A client whose requests are authenticated as the given user, holding the given API
    /// permissions, as if their access token said so. The user doesn't have to exist.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(UserId userId, params string[] permissions)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.Value.ToString());
        client.DefaultRequestHeaders.Add("X-Forwarded-For", NewClientAddress());
        if (permissions.Length > 0)
        {
            client.DefaultRequestHeaders.Add(
                TestAuthHandler.PermissionsHeader,
                string.Join(',', permissions)
            );
        }
        return client;
    }

    /// <summary>Saves a user who can sign in with <see cref="DefaultPassword"/>.</summary>
    public async Task<User> CreateUserAsync(
        UserRole role = UserRole.Member,
        string? email = null,
        string name = "Ada Lovelace"
    )
    {
        using var scope = Services.CreateScope();
        var passwordHash = scope
            .ServiceProvider.GetRequiredService<IPasswordHasher>()
            .Hash(DefaultPassword);
        return await scope
            .ServiceProvider.GetRequiredService<IRepository<User>>()
            .AddAsync(
                User.Create(
                    UserName.From(name),
                    new EmailAddress(email ?? $"user-{Guid.NewGuid():N}@example.com"),
                    passwordHash,
                    role
                )
            );
    }

    /// <summary>A signed-in client for a new user, as <see cref="CreateAuthenticatedClient"/>.</summary>
    public async Task<(User User, HttpClient Client)> CreateSignedInUserAsync(
        params string[] permissions
    )
    {
        var user = await CreateUserAsync();
        return (user, CreateAuthenticatedClient(user.Id, permissions));
    }
}
