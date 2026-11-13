using AppTemplate.Core.Interfaces;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Interfaces;
using AppTemplate.Web.Services;

namespace AppTemplate.Web.Configurations;

public static class AuthenticationConfigurations
{
    /// <summary>
    /// Authorization and the current user. Authentication is ASP.NET Core Identity's session
    /// cookie, registered with Identity in Infrastructure (ADR 015); FastEndpoints endpoints
    /// require it unless they call <c>AllowAnonymous()</c>, and <c>Permissions(...)</c> checks the
    /// <see cref="AppClaimTypes.Permission"/> claims it carries.
    /// </summary>
    public static IServiceCollection AddAuthenticationConfigurations(
        this IServiceCollection services,
        ILoggerAdapter logger
    )
    {
        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        logger.LogInformation(
            "{Project} were configured ({Permissions} permissions)",
            "Authentication and authorization",
            Permission.List.Count
        );

        return services;
    }
}
