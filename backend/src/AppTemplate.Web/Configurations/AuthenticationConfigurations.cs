using AppTemplate.ServiceDefaults;

namespace AppTemplate.Web.Configurations;

public static class AuthenticationConfigurations
{
    /// <summary>
    /// The API only accepts OAuth 2.0 access tokens (JWTs) issued by Keycloak for this API's
    /// audience. Browsers never call it directly - AppTemplate.BackendForFrontend holds the
    /// user's session cookie and attaches the access token as a Bearer header when proxying.
    /// See ADR 007.
    /// </summary>
    public static IServiceCollection AddAuthenticationConfigurations(
        this IServiceCollection services,
        Microsoft.Extensions.Logging.ILogger logger,
        WebApplicationBuilder builder
    )
    {
        var keycloak = builder.Configuration.GetSection("Keycloak");
        // Required: read here so a missing value stops startup, not the first request.
        var realm = builder.Configuration.GetRequiredValue("Keycloak:Realm");
        var audience = builder.Configuration.GetRequiredValue("Keycloak:Audience");

        services
            .AddAuthentication()
            .AddKeycloakJwtBearer(
                serviceName: "keycloak",
                realm: realm,
                options =>
                {
                    options.Audience = audience;
                    // Keycloak runs over plain HTTP inside Aspire locally.
                    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                    // The Aspire service-discovery address is for local development. Elsewhere,
                    // set Keycloak:Authority to the realm's public HTTPS URL (the tokens' issuer).
                    if (keycloak["Authority"] is { Length: > 0 } authority)
                    {
                        options.Authority = authority;
                    }
                    // Keep JWT claim names ("sub", "email", ...) instead of the legacy
                    // WS-Federation URIs ASP.NET Core maps them to by default.
                    options.MapInboundClaims = false;
                }
            );

        logger.LogInformation("{Project} were configured", "Authentication");

        return services;
    }
}
