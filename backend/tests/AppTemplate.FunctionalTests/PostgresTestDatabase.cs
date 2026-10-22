using Npgsql;
using Testcontainers.PostgreSql;

namespace AppTemplate.FunctionalTests;

/// <summary>Trait for tests that need Docker (a real Postgres) - CI filters these out where Docker can't run.</summary>
public static class TestCategories
{
    public const string Name = "Category";
    public const string RequiresDocker = "RequiresDocker";
}

/// <summary>
/// One real Postgres container for the whole test run, started on first use (Testcontainers'
/// reaper removes it when the run ends). Each test host gets its own database inside it, so
/// hosts stay isolated without a container each.
/// </summary>
public static class PostgresTestDatabase
{
    // Same major version the AppHost runs (Aspire.Hosting.PostgreSQL's default image).
    private const string Image = "postgres:18.3";

    private static readonly Lazy<Task<PostgreSqlContainer>> Container = new(StartAsync);

    /// <summary>A connection string to a new, not-yet-created database (migrations create it).</summary>
    public static async Task<string> NewDatabaseConnectionStringAsync()
    {
        var container = await Container.Value;
        return new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = $"apptemplate_tests_{Guid.NewGuid():N}",
        }.ConnectionString;
    }

    private static async Task<PostgreSqlContainer> StartAsync()
    {
        var container = new PostgreSqlBuilder(Image).Build();
        try
        {
            await container.StartAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Functional tests run against a real Postgres in Docker (Testcontainers), and it "
                    + "couldn't be started - is Docker running? To run only the tests that "
                    + $"don't need it: dotnet test --filter \"{TestCategories.Name}!={TestCategories.RequiresDocker}\"",
                ex
            );
        }
        return container;
    }
}
