using System.Collections.Concurrent;
using AppTemplate.Core.Interfaces;
using AppTemplate.Infrastructure;
using AppTemplate.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace AppTemplate.IntegrationTests.Data;

/// <summary>
/// One fresh Postgres database per test class, built by the real migrations, with the
/// Infrastructure services registered exactly as the app registers them. Postgres rather than EF
/// Core InMemory, as ADR 006 explains: unique indexes, cascades and raw SQL behave as in
/// production. Domain events go to a substitute dispatcher, and emails to <see cref="Emails"/>.
/// </summary>
public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    public const string ClientUrl = "https://app.test";

    public ServiceProvider Services { get; private set; } = null!;

    public RecordingEmailSender Emails { get; } = new();

    public async ValueTask InitializeAsync()
    {
        Services = await ServicesForAsync(
            await PostgresTestDatabase.NewDatabaseConnectionStringAsync(),
            Emails
        );

        using var scope = Services.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
            .Database.MigrateAsync();
    }

    /// <summary>The Infrastructure services on a database: one per instance of the app.</summary>
    public static Task<ServiceProvider> ServicesForAsync(
        string connectionString,
        RecordingEmailSender emails
    )
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration["ConnectionStrings:apptemplate"] = connectionString;
        builder.Services.AddLogging();
        builder.AddInfrastructureServices(builder.Configuration, NullLogger.Instance);

        var services = builder.Services;
        services.Replace(ServiceDescriptor.Scoped(_ => Substitute.For<IDomainEventDispatcher>()));
        // What Web provides: the email sender, account links, and the authentication services
        // SignInManager is built with (it issues nothing here).
        services.AddSingleton<IEmailSender>(emails);
        services.Configure<AccountLinkSettings>(links => links.ClientUrl = ClientUrl);
        services.AddAuthentication();
        services.AddHttpContextAccessor();
        return Task.FromResult(services.BuildServiceProvider());
    }

    public async ValueTask DisposeAsync() => await Services.DisposeAsync();
}

/// <summary>
/// ardalis's BaseEfRepoTestFixture, on Postgres: each test gets its own scope (DbContext and
/// repositories) on the class's database. Tests share that database, so they use their own data.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public abstract class BaseEfRepoTestFixture(PostgresDatabaseFixture database)
    : IClassFixture<PostgresDatabaseFixture>,
        IDisposable
{
    private readonly IServiceScope _scope = database.Services.CreateScope();

    protected IServiceProvider Services => _scope.ServiceProvider;

    protected ApplicationDatabaseContext DbContext =>
        Services.GetRequiredService<ApplicationDatabaseContext>();

    protected IRepository<T> GetRepository<T>()
        where T : class, IAggregateRoot => Services.GetRequiredService<IRepository<T>>();

    /// <summary>A second, independent scope: another request, with its own DbContext.</summary>
    protected IServiceScope NewScope() => database.Services.CreateScope();

    protected RecordingEmailSender Emails => database.Emails;

    protected static User NewUser() => User.Create(UserName.From("Ada Lovelace"));

    protected static EmailAddress NewEmail() => new($"user-{Guid.NewGuid():N}@example.com");

    public void Dispose()
    {
        _scope.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Records sent emails, so tests can follow the links in them.</summary>
public sealed class RecordingEmailSender : IEmailSender
{
    public ConcurrentQueue<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task SendAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        Sent.Enqueue((to, subject, body));
        return Task.CompletedTask;
    }

    /// <summary>The last link sent to an address, from <see cref="PostgresDatabaseFixture.ClientUrl"/>.</summary>
    public Uri LastLinkTo(string to) =>
        new(
            Sent.Last(email => email.To == to)
                .Body.Split(['\n', ' '])
                .Single(word =>
                    word.StartsWith(PostgresDatabaseFixture.ClientUrl, StringComparison.Ordinal)
                )
        );
}
