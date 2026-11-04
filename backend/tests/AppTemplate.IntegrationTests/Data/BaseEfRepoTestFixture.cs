using AppTemplate.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AppTemplate.IntegrationTests.Data;

/// <summary>
/// One fresh Postgres database per test class, built by the real migrations, with the
/// Infrastructure services registered exactly as the app registers them. Postgres rather than EF
/// Core InMemory, as ADR 006 explains: unique indexes, cascades and raw SQL behave as in
/// production. Domain events go to a substitute dispatcher.
/// </summary>
public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    public ServiceProvider Services { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:apptemplate"] =
            await PostgresTestDatabase.NewDatabaseConnectionStringAsync();

        var services = new ServiceCollection().AddLogging();
        services.AddInfrastructureServices(configuration, NullLogger.Instance);
        services.Replace(ServiceDescriptor.Scoped(_ => Substitute.For<IDomainEventDispatcher>()));
        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
            .Database.MigrateAsync();
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

    protected static User NewUser(string? email = null) =>
        User.Create(
            UserName.From("Ada Lovelace"),
            new EmailAddress(email ?? $"user-{Guid.NewGuid():N}@example.com"),
            "hashed-password"
        );

    protected static RefreshToken NewRefreshToken(UserId userId) =>
        RefreshToken.Create(
            userId,
            Convert.ToHexStringLower(
                Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray()
            ),
            DateTimeOffset.UtcNow.AddDays(7)
        );

    public void Dispose() => _scope.Dispose();
}
