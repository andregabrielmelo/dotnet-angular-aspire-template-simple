using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Authentication;
using AppTemplate.Web.Authorization;
using AppTemplate.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppTemplate.Web.Configurations;

public static class AuthConfigurations
{
    /// <summary>
    /// JWT bearer authentication with FastEndpoints.Security, as its docs and demos set it up
    /// (https://fast-endpoints.com/docs/security). Access tokens are sent as
    /// <c>Authorization: Bearer</c>; refresh tokens travel in an HttpOnly cookie
    /// (<see cref="RefreshTokenCookie"/>). See ADR 012.
    /// </summary>
    public static IServiceCollection AddAuthConfigurations(
        this IServiceCollection services,
        Microsoft.Extensions.Logging.ILogger logger,
        WebApplicationBuilder builder
    )
    {
        // Read here, as FastEndpoints' docs do. OptionConfigurations validates the same section on
        // start, so a missing or short signing key fails startup before any token is issued.
        var jwt =
            builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new();

        services
            .AddAuthenticationJwtBearer(
                signing => signing.SigningKey = jwt.SigningKey,
                // FastEndpoints validates the issuer and audience once they're set, and keeps JWT
                // claim names ("sub", "email", ...) instead of the legacy WS-Federation URIs.
                bearer =>
                {
                    bearer.TokenValidationParameters.ValidIssuer = jwt.Issuer;
                    bearer.TokenValidationParameters.ValidAudience = jwt.Audience;
                }
            )
            // FastEndpoints endpoints are secure by default. The fallback policy only covers routes
            // mapped outside them: FastEndpoints 8.3 maps GET /_test_url_cache_ with a plain MapGet
            // and no authorization metadata, and it lists every route and endpoint type name. Drop
            // this once on FastEndpoints 8.4+, where that route is opt-in.
            .AddAuthorization(options =>
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build()
            )
            // Global token creation settings (FastEndpoints docs, "Generating JWT Tokens"); the
            // token service picks them up, so it only sets the validities.
            .Configure<JwtCreationOptions>(options =>
            {
                options.SigningKey = jwt.SigningKey;
                options.Issuer = jwt.Issuer;
                options.Audience = jwt.Audience;
            });

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddSingleton<RevokedAccessTokens>();
        services.AddSingleton<RefreshTokenCookie>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        logger.LogInformation(
            "{Project} were configured ({Permissions} permissions)",
            "Authentication and authorization",
            Permission.All.Count
        );

        return services;
    }
}
