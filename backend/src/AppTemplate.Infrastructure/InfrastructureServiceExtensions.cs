using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Data.Queries;
using AppTemplate.UseCases.Users.List;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AppTemplate.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public const string PostgresHealthCheckName = "postgres";

    // ServiceDefaults' HealthCheckTags.Ready; Infrastructure doesn't reference ServiceDefaults.
    private const string ReadyHealthCheckTag = "ready";

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfigurationManager config,
        ILogger logger
    )
    {
        string? connectionString = config.GetConnectionString("apptemplate");
        if (connectionString is null)
        {
            throw new InvalidOperationException(
                "No valid connection string found. Please ensure that the 'apptemplate' is configured."
            );
        }

        services.AddScoped<ISaveChangesInterceptor, EventDispatchInterceptor>();
        services.AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>();

        services.AddDbContext<ApplicationDatabaseContext>(
            (provider, options) =>
            {
                // Use PostgreSQL as the database provider
                options
                    .UseNpgsql(
                        connectionString,
                        options => options.MigrationsHistoryTable("__EFMigrationsHistory", "wfc")
                    )
                    .UseSnakeCaseNamingConvention();

                // Post-commit domain event dispatch.
                options.AddInterceptors(provider.GetServices<ISaveChangesInterceptor>());

                // TODO: Reavaluate the need for this
                // Change PendingModelChangesWarning to Log to avoid exceptions when the model changes without a new migration
                // workround to bug when starting a new database with initial migration
                options.ConfigureWarnings(warnings =>
                    warnings.Log(RelationalEventId.PendingModelChangesWarning)
                );
            }
        );

        // Postgres is the one required dependency: without it this instance can't serve
        // requests, so it gates readiness (/health). See best-practices.md, Health checks.
        services
            .AddHealthChecks()
            .AddDbContextCheck<ApplicationDatabaseContext>(
                PostgresHealthCheckName,
                tags: [ReadyHealthCheckTag],
                customTestQuery: (context, cancellationToken) =>
                    context.Database.CanConnectAsync(cancellationToken)
            );
        // An unreachable host would otherwise hold the probe for Npgsql's 15-second timeout.
        services.Configure<HealthCheckServiceOptions>(options =>
            options.Registrations.Single(r => r.Name == PostgresHealthCheckName).Timeout =
                TimeSpan.FromSeconds(5)
        );

        services
            .AddScoped(typeof(IRepository<>), typeof(EntityFrameworkRepository<>))
            .AddScoped<IListUsersQueryService, ListUsersQueryService>();

        logger.LogInformation("{Project} services registered", "Infrastructure");

        return services;
    }
}
