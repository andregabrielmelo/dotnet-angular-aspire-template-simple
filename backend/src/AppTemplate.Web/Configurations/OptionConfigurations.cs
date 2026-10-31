using AppTemplate.Core.ValueObjects;
using AppTemplate.Infrastructure.Email;
using AppTemplate.Web.Authentication;

namespace AppTemplate.Web.Configurations;

public static class OptionConfigurations
{
    public static IServiceCollection AddOptionConfigurations(
        this IServiceCollection services,
        IConfiguration configuration,
        Microsoft.Extensions.Logging.ILogger logger
    )
    {
        services
            .AddOptions<MailserverConfiguration>()
            .Bind(configuration.GetSection(MailserverConfiguration.SectionName))
            .Validate(
                mail =>
                    !string.IsNullOrWhiteSpace(mail.Hostname)
                    && mail.Port is > 0 and <= 65535
                    && EmailAddress.IsValid(mail.FromAddress),
                "Mailserver needs a Hostname, a Port between 1 and 65535, and a FromAddress."
            )
            .ValidateOnStart();

        services
            .AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(
                jwt =>
                    !string.IsNullOrWhiteSpace(jwt.Issuer)
                    && !string.IsNullOrWhiteSpace(jwt.Audience),
                "Jwt needs an Issuer and an Audience."
            )
            .Validate(
                jwt => JwtSettings.IsValidSigningKey(jwt.SigningKey),
                $"Jwt:SigningKey must be base64 of at least {JwtSettings.MinimumSigningKeyBytes} random bytes (openssl rand -base64 64)."
            )
            .Validate(
                jwt =>
                    jwt.AccessTokenLifetime > TimeSpan.Zero
                    && jwt.RefreshTokenReuseGracePeriod >= TimeSpan.Zero
                    && jwt.RefreshTokenSlidingLifetime > jwt.AccessTokenLifetime
                    && jwt.RefreshTokenAbsoluteLifetime >= jwt.RefreshTokenSlidingLifetime,
                "Jwt lifetimes must be positive, with access < sliding refresh <= absolute refresh."
            )
            .ValidateOnStart();

        services
            .AddOptions<AuthCookieSettings>()
            .Bind(configuration.GetSection(AuthCookieSettings.SectionName))
            .Validate(
                cookies =>
                    cookies.AllowedOrigins.Length > 0
                    && cookies.AllowedOrigins.All(AuthCookieSettings.IsOrigin),
                "Authentication:AllowedOrigins needs at least one origin, such as https://app.example.com (no path, no trailing slash)."
            )
            .Validate(
                cookies => cookies.RefreshCookiePath.StartsWith('/'),
                "Authentication:RefreshCookiePath must start with '/'."
            )
            .ValidateOnStart();

        logger.LogInformation("{Project} were configured", "Options");

        return services;
    }
}
