using AppTemplate.UseCases.Authentication;
using AppTemplate.Web.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AppTemplate.Web.Configurations;

public static class AuthenticationConfigurations
{
    /// <summary>
    /// The API issues its own tokens (ADR 007): a short-lived JWT access token, sent as
    /// <c>Authorization: Bearer</c>, and a rotating refresh token in an HttpOnly cookie
    /// (<see cref="AuthCookies"/>). Every part of the JWT is validated explicitly: signature and
    /// algorithm, issuer, audience and lifetime.
    /// </summary>
    public static IServiceCollection AddAuthenticationConfigurations(
        this IServiceCollection services,
        Microsoft.Extensions.Logging.ILogger logger,
        WebApplicationBuilder builder
    )
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        // Read from the registered settings when the options are built, never at registration.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>(
                (options, registered) =>
                {
                    var jwt = registered.Value;
                    // Keep JWT claim names ("sub", "email", ...) instead of the legacy
                    // WS-Federation URIs ASP.NET Core maps them to by default.
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(jwt.SigningKeyBytes()),
                        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        NameClaimType = JwtRegisteredClaimNames.Name,
                    };
                }
            );

        var isDevelopment = builder.Environment.IsDevelopment();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = AuthCookies.AntiforgeryHeaderName;
            options.Cookie.Name = "apptemplate_antiforgery";
            options.Cookie.SecurePolicy = isDevelopment
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton(provider =>
        {
            var jwt = provider.GetRequiredService<IOptions<JwtSettings>>().Value;
            return new RefreshTokenOptions(
                jwt.RefreshTokenSlidingLifetime,
                jwt.RefreshTokenAbsoluteLifetime,
                jwt.RefreshTokenReuseGracePeriod
            );
        });
        services.AddScoped<SessionFactory>();
        services.AddScoped<AuthCookies>();

        logger.LogInformation("{Project} were configured", "Authentication");

        return services;
    }
}
