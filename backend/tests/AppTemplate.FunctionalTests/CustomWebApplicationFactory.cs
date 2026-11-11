using AppTemplate.Core.ValueObjects;
using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.UseCases.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace AppTemplate.FunctionalTests;

/// <summary>
/// The real API - HTTP -> FastEndpoints -> Mediator -> EF Core -> Postgres - against a fresh
/// database in the shared Postgres container (<see cref="PostgresTestDatabase"/>), built by the
/// real migrations. Tests sign in for real (<see cref="TestSession"/>), with Identity's session
/// cookie. Emails go to <see cref="EmailSender"/>, and <see cref="Time"/> is the clock the cookie
/// and its security stamp checks are timed with.
/// </summary>
public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
{
    /// <summary>The password of users made by <see cref="CreateUserAsync"/>.</summary>
    public const string Password = "correct horse battery staple";

    /// <summary>Where account emails link to (AccountLinks:ClientUrl).</summary>
    public const string ClientUrl = "https://app.test";

    protected string? ConnectionString { get; set; }

    public FakeEmailSender EmailSender { get; } = new();

    /// <summary>Starts at the real time; only moves when a test advances it.</summary>
    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    public virtual async ValueTask InitializeAsync()
    {
        ConnectionString = await PostgresTestDatabase.NewDatabaseConnectionStringAsync();

        using var scope = Services.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
            .Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // not Development: no seed data, Secure cookies
        builder.UseSetting(
            "ConnectionStrings:apptemplate",
            ConnectionString
                ?? throw new InvalidOperationException(
                    "The factory is used before InitializeAsync - use it as an xUnit fixture."
                )
        );
        builder.UseSetting("Mailserver:FromAddress", "noreply@app.test");
        builder.UseSetting("AccountLinks:ClientUrl", ClientUrl);
        // Tests sign in many times from one address; RateLimitingTests set their own limits.
        builder.UseSetting("RateLimiting:AnonymousPermitLimit", "100000");
        builder.UseSetting("RateLimiting:AuthenticatedPermitLimit", "100000");
        builder.UseSetting("RateLimiting:AuthenticationPermitLimit", "100000");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);

            // Hashing is deliberately slow; tests create many accounts.
            services.Configure<PasswordHasherOptions>(options => options.IterationCount = 1_000);
        });
    }

    /// <summary>
    /// Saves a user (profile and confirmed account, through <see cref="IdentityService"/>) who can
    /// sign in with <see cref="Password"/>; a <see cref="Role.Member"/> unless told otherwise.
    /// </summary>
    public async Task<TestUser> CreateUserAsync(Role? role = null, string? email = null)
    {
        using var scope = Services.CreateScope();
        var address = new EmailAddress(email ?? $"user-{Guid.NewGuid():N}@example.com");
        var name = UserName.From("Ada Lovelace");
        var created = await scope
            .ServiceProvider.GetRequiredService<IIdentityService>()
            .CreateUserAsync(
                name,
                address,
                Password,
                role ?? Role.Member,
                emailConfirmed: true,
                CancellationToken.None
            );
        created.IsSuccess.ShouldBeTrue(string.Join("; ", created.Errors));
        return new TestUser(created.Value, name, address);
    }

    /// <summary>Gives the user's account another role, as an administrator would.</summary>
    public async Task AddToRoleAsync(TestUser user, Role role)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var account = await users.Users.SingleAsync(account => account.ProfileId == user.Id);
        (await users.AddToRoleAsync(account, role.Name)).Succeeded.ShouldBeTrue();
    }
}

/// <summary>A user made by <see cref="CustomWebApplicationFactory{TProgram}.CreateUserAsync"/>.</summary>
public sealed record TestUser(UserId Id, UserName Name, EmailAddress Email);

public static class CustomWebApplicationFactoryExtensions
{
    /// <summary>A new user, signed in: the session's client sends their session cookie.</summary>
    public static async Task<(TestUser User, TestSession Session)> CreateSignedInSessionAsync(
        this CustomWebApplicationFactory<Program> factory,
        Role? role = null
    )
    {
        var user = await factory.CreateUserAsync(role);
        var session = new TestSession(factory);
        (await session.LoginAsync(user.Email.Value)).EnsureSuccessStatusCode();
        return (user, session);
    }
}
