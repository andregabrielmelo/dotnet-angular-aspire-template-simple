using AppTemplate.Infrastructure.Data;
using AppTemplate.UseCases.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AppTemplate.Web.Configurations;

public static class DatabaseConfigurations
{
    public static async Task<IApplicationBuilder> StartDatabase(this WebApplication app)
    {
        // Migrate in Development, or anywhere Database:ApplyMigrationsOnStartup is set. Seed data
        // is demo fixtures, so it's only ever added in Development.
        var shouldMigrate =
            app.Environment.IsDevelopment()
            || app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");

        if (shouldMigrate)
        {
            await MigrateDatabaseAsync(app);
        }

        if (app.Environment.IsDevelopment())
        {
            await SeedDatabaseAsync(app);
        }

        return app;
    }

    static async Task MigrateDatabaseAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        var context = services.GetRequiredService<ApplicationDatabaseContext>();

        try
        {
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                StartupLog.ApplyingMigrations(logger);
                foreach (var migration in pendingMigrations)
                {
                    StartupLog.PendingMigration(logger, migration);
                }

                await context.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            StartupLog.MigrationFailed(logger, ex, ex.Message);
            throw; // Re-throw to make startup fail if migrations fail
        }
    }

    static async Task SeedDatabaseAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();

        try
        {
            StartupLog.SeedingDatabase(logger);
            var context = services.GetRequiredService<ApplicationDatabaseContext>();
            await SeedData.InitializeAsync(
                context,
                services.GetRequiredService<IIdentityService>()
            );
            StartupLog.DatabaseSeeded(logger);
        }
        catch (Exception ex)
        {
            StartupLog.SeedingFailed(logger, ex, ex.Message);
            // Don't re-throw for seeding errors - it's not critical
        }
    }
}
