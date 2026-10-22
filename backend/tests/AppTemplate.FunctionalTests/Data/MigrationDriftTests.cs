using AppTemplate.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AppTemplate.FunctionalTests.Data;

/// <summary>
/// The migrations are the schema. A model change without a migration fails here, and so does a
/// migration that was edited by hand until the schema it builds no longer matches the model.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class MigrationDriftTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    [Fact]
    public void Model_HasNoChangesMissingAMigration()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDatabaseContext>();

        Assert.False(
            context.Database.HasPendingModelChanges(),
            "The EF Core model has changes no migration captures. Add one: dotnet ef migrations "
                + "add <Name> --project src/AppTemplate.Infrastructure "
                + "--startup-project src/AppTemplate.Web -o Data/Migrations"
        );
    }

    [Fact]
    public async Task Migrations_BuildTheSameSchemaAsTheModel()
    {
        var migrated = await PostgresTestDatabase.NewDatabaseConnectionStringAsync();
        var fromModel = await PostgresTestDatabase.NewDatabaseConnectionStringAsync();

        await WithContextAsync(migrated, context => context.Database.MigrateAsync());
        await WithContextAsync(fromModel, context => context.Database.EnsureCreatedAsync());

        var expected = await SchemaOfAsync(fromModel);
        var actual = await SchemaOfAsync(migrated);

        var missing = expected.Except(actual).ToList();
        var extra = actual.Except(expected).ToList();
        Assert.True(
            missing.Count == 0 && extra.Count == 0,
            "The migrated schema differs from the model.\n"
                + $"Only in the model:\n  {string.Join("\n  ", missing)}\n"
                + $"Only in the migrations:\n  {string.Join("\n  ", extra)}"
        );
    }

    /// <summary>The app's own context configuration (provider, naming convention), on another database.</summary>
    private async Task WithContextAsync(
        string connectionString,
        Func<ApplicationDatabaseContext, Task> action
    )
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDatabaseContext>();
        context.Database.SetConnectionString(connectionString);
        await action(context);
    }

    /// <summary>Every column, constraint and index in the public schema, as sorted lines.</summary>
    private static async Task<List<string>> SchemaOfAsync(string connectionString)
    {
        const string sql = """
            SELECT 'column ' || rel.relname || '.' || att.attname || ' '
                || format_type(att.atttypid, att.atttypmod)
                || ' notnull=' || att.attnotnull || ' identity=' || att.attidentity::text
                || ' default=' || coalesce(pg_get_expr(def.adbin, def.adrelid), '')
            FROM pg_attribute att
            JOIN pg_class rel ON rel.oid = att.attrelid AND rel.relkind = 'r'
            JOIN pg_namespace ns ON ns.oid = rel.relnamespace
            LEFT JOIN pg_attrdef def ON def.adrelid = att.attrelid AND def.adnum = att.attnum
            WHERE ns.nspname = 'public' AND rel.relname <> '__EFMigrationsHistory'
                AND att.attnum > 0 AND NOT att.attisdropped
            UNION ALL
            SELECT 'constraint ' || rel.relname || '.' || con.conname || ' ' || pg_get_constraintdef(con.oid)
            FROM pg_constraint con
            JOIN pg_class rel ON rel.oid = con.conrelid
            JOIN pg_namespace ns ON ns.oid = rel.relnamespace
            WHERE ns.nspname = 'public' AND rel.relname <> '__EFMigrationsHistory'
            UNION ALL
            SELECT 'index ' || indexdef
            FROM pg_indexes
            WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var lines = new List<string>();
        while (await reader.ReadAsync())
        {
            lines.Add(reader.GetString(0));
        }
        lines.Sort(StringComparer.Ordinal);
        return lines;
    }
}
