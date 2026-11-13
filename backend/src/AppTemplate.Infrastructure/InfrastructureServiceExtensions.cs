using AppTemplate.Core.Interfaces;
using AppTemplate.Infrastructure.Caching;
using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Data.Queries;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.Infrastructure.Logging;
using AppTemplate.UseCases.Constants;
using AppTemplate.UseCases.Interfaces;
using AppTemplate.UseCases.Users.Queries.List;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AppTemplate.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public const string RedisConnectionName = "cache";

    public static IServiceCollection AddInfrastructureServices(
        this IHostApplicationBuilder builder,
        IConfigurationManager config,
        ILoggerAdapter logger
    )
    {
        // Set by the AppHost (WithReference) in development, and by the environment elsewhere.
        var connectionString = Guard.Against.NullOrEmpty(
            config.GetConnectionString("apptemplate"),
            "ConnectionStrings:apptemplate"
        );
        // Every layer logs through ILoggerAdapter<T> (Core), which this adapter forwards to
        // Microsoft.Extensions.Logging's providers.
        builder.Services.AddSingleton(typeof(ILoggerAdapter<>), typeof(LoggerAdapter<>));

        builder.Services.AddScoped<ISaveChangesInterceptor, EventDispatchInterceptor>();
        builder.Services.AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>();

        builder.Services.AddDbContext<ApplicationDatabaseContext>(
            (provider, options) =>
            {
                // Use PostgreSQL as the database provider
                options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();

                // Post-commit domain event dispatch.
                options.AddInterceptors(provider.GetServices<ISaveChangesInterceptor>());

                options.ConfigureWarnings(warnings =>
                    warnings.Log(RelationalEventId.PendingModelChangesWarning)
                );
            }
        );

        // Aspire's client integration: health check, logging, traces and metrics for the context,
        // and a retrying execution strategy (code that opens its own transaction runs it inside one).
        builder.EnrichNpgsqlDbContext<ApplicationDatabaseContext>();

        builder
            .Services.AddScoped(typeof(IRepository<>), typeof(EntityFrameworkRepository<>))
            .AddScoped<IListUsersQueryService, ListUsersQueryService>();

        // HybridCache, with Redis as L2 when the AppHost provides it (ADR 017).
        var useRedis = !string.IsNullOrEmpty(
            builder.Configuration.GetConnectionString(RedisConnectionName)
        );
        if (useRedis)
        {
            // HybridCache logs and skips a failing L2 (falling back to L1 and the handler), so
            // Redis being down is never an error. While the client knows it's disconnected,
            // FailFast fails each command at once instead of queueing it until the timeout;
            // the client reconnects by itself. Aspire's own Redis health check is off: it would
            // fail /health for an optional dependency.
            builder.AddRedisDistributedCache(
                RedisConnectionName,
                settings => settings.DisableHealthChecks = true,
                options => options.BacklogPolicy = BacklogPolicy.FailFast
            );
        }

        builder.Services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = 1024 * 1024;
            options.MaximumKeyLength = 512;
        });
        builder
            .Services.AddOptions<HybridCacheOptions>()
            .Configure<IOptions<CachingSettings>>(
                (options, settings) =>
                    options.DefaultEntryOptions = new HybridCacheEntryOptions
                    {
                        Expiration = settings.Value.Expiration,
                        LocalCacheExpiration = settings.Value.LocalExpiration,
                    }
            );

        logger.LogInformation(
            "{Project} were configured",
            useRedis ? "HybridCache with Redis" : "HybridCache in memory"
        );

        // Confirmation and reset codes are protected with Data Protection's keys; keeping them in
        // Postgres makes links survive restarts and work on every instance.
        // The application name keeps every instance on the same key purposes, whatever its
        // content root.
        builder
            .Services.AddDataProtection()
            .SetApplicationName("AppTemplate")
            .PersistKeysToDbContext<ApplicationDatabaseContext>();

        // ASP.NET Core Identity for accounts (ADR 013) and sessions (ADR 015): AddIdentity
        // registers its cookie schemes, makes the application cookie the default scheme, and
        // validates each cookie's security stamp.
        builder
            .Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // The user name is the email, which EmailAddress has already validated.
                options.User.AllowedUserNameCharacters = string.Empty;
                options.SignIn.RequireConfirmedEmail = true;

                // Length only, no composition rules (NIST SP 800-63B).
                options.Password.RequiredLength = PasswordConstants.MIN_LENGTH;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<ApplicationDatabaseContext>()
            .AddClaimsPrincipalFactory<ApplicationClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();

        // The session cookie, set for an API: Identity's defaults otherwise.
        builder.Services.ConfigureApplicationCookie(options =>
        {
            // Never sent on a cross-site request; with no CORS, this is the CSRF defense (ADR 015).
            options.Cookie.SameSite = SameSiteMode.Strict;
            // Every request is HTTPS, development included (ADR 016).
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            // An API answers 401 and 403; it doesn't redirect to a login page.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        // Bearer tokens for non-browser clients, next to the cookie (ADR 018).
        builder.Services.AddTokenAuthentication();

        builder
            .Services.AddScoped<IEmailSender<ApplicationUser>, AccountEmailSender>()
            .AddTransient<IIdentityService, IdentityService>()
            .AddTransient<ITokenClaimsService, IdentityTokenClaimService>();

        logger.LogInformation("{Project} services registered", "Infrastructure");

        return builder.Services;
    }
}
