using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Data.Queries;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.UseCases.Constants;
using AppTemplate.UseCases.Interfaces;
using AppTemplate.UseCases.Users.Queries.List;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppTemplate.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IHostApplicationBuilder builder,
        IConfigurationManager config,
        ILogger logger
    )
    {
        // Set by the AppHost (WithReference) in development, and by the environment elsewhere.
        var connectionString = Guard.Against.NullOrEmpty(
            config.GetConnectionString("apptemplate"),
            "ConnectionStrings:apptemplate"
        );
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
        // and a retrying execution strategy (IdentityService runs its own transaction inside it).
        builder.EnrichNpgsqlDbContext<ApplicationDatabaseContext>();

        builder
            .Services.AddScoped(typeof(IRepository<>), typeof(EntityFrameworkRepository<>))
            .AddScoped<IListUsersQueryService, ListUsersQueryService>();

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

        builder
            .Services.AddScoped<IEmailSender<ApplicationUser>, AccountEmailSender>()
            .AddTransient<IIdentityService, IdentityService>();

        logger.LogInformation("{Project} services registered", "Infrastructure");

        return builder.Services;
    }
}
