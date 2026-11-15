using Npgsql;
using Testcontainers.PostgreSql;

namespace AppTemplate.Tests;

/// <summary>
/// One real Postgres container for the whole test run, started on first use and removed when the
/// test process exits. Testcontainers' reaper (Ryuk) would do that too, but it's off under Podman
/// (TESTCONTAINERS_RYUK_DISABLED), so without this every run left a container behind. Each test
/// host gets its own database inside it, so hosts stay isolated without a container each.
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
                "These tests run against a real Postgres in Docker (Testcontainers), and it "
                    + "couldn't be started - is Docker running? To run only the tests that "
                    + $"don't need it: dotnet test --filter \"{TestCategories.Name}!={TestCategories.RequiresDocker}\"",
                ex
            );
        }

        // A static container has no fixture to dispose it, so the process does it on exit.
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            container.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return container;
    }
}
