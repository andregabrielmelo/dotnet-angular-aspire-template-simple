using AppTemplate.ServiceDefaults;
using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace AppTemplate.Web.Configurations;

public static class AuthorizationConfigurations
{
    /// <summary>
    /// One policy per permission (policy name = permission name), satisfied by the
    /// matching permission claim that <see cref="KeycloakPermissionsClaimsTransformation"/>
    /// derives from the access token. Endpoints opt in with FastEndpoints' <c>Policies(...)</c>;
    /// everything else only requires an authenticated user (FastEndpoints' default, and the
    /// fallback policy for routes that declare nothing).
    /// </summary>
    public static IServiceCollection AddAuthorizationConfigurations(
        this IServiceCollection services,
        Microsoft.Extensions.Logging.ILogger logger,
        WebApplicationBuilder builder
    )
    {
        // Required: read here so a missing value stops startup, not the first request.
        var apiClientId = builder.Configuration.GetRequiredValue("Keycloak:Audience");
        services
            .AddOptions<KeycloakAuthorizationOptions>()
            // The API's client id is the token audience: one required setting for both.
            .Configure(options => options.ApiClientId = apiClientId);
        services.AddTransient<IClaimsTransformation, KeycloakPermissionsClaimsTransformation>();

        // Routes without authorization metadata (anything mapped outside FastEndpoints that
        // forgets RequireAuthorization/AllowAnonymous) require a signed-in user instead of being
        // public. FastEndpoints 8.2/8.3 itself maps one: GET /_test_url_cache_, which lists every
        // route and endpoint type name (opt-in only from FastEndpoints 8.4).
        var authorization = services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        foreach (var permission in Permission.All)
        {
            authorization.AddPolicy(
                permission,
                policy =>
                    policy
                        .RequireAuthenticatedUser()
                        .RequireClaim(
                            KeycloakPermissionsClaimsTransformation.PermissionClaimType,
                            permission
                        )
            );
        }

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        logger.LogInformation("{Project} were configured", "Permission policies");

        return services;
    }
}
