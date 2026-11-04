using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Data.Queries;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Users.List;
using Microsoft.AspNetCore.Identity;
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

        AddIdentity(services);

        logger.LogInformation("{Project} services registered", "Infrastructure");

        return services;
    }

    /// <summary>
    /// ASP.NET Core Identity for accounts (ADR 013): Identity Core only, so it registers no cookie
    /// scheme, and the API keeps signing in with FastEndpoints' JWTs. SignInManager is there for
    /// its password check with lockout, not to sign anyone in.
    /// </summary>
    private static void AddIdentity(IServiceCollection services)
    {
        // Confirmation and reset codes are protected with Data Protection's keys; see
        // best-practices.md, Production deployment, for keeping them across restarts.
        services.AddDataProtection();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // The user name is the email, which EmailAddress has already validated.
                options.User.AllowedUserNameCharacters = string.Empty;
                options.SignIn.RequireConfirmedEmail = true;

                // Length only, no composition rules (NIST SP 800-63B).
                options.Password.RequiredLength = PasswordRules.MinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDatabaseContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services
            .AddScoped<IEmailSender<ApplicationUser>, AccountEmailSender>()
            .AddScoped<IdentityService>()
            .AddScoped<IIdentityService>(provider =>
                provider.GetRequiredService<IdentityService>()
            );
    }
}
