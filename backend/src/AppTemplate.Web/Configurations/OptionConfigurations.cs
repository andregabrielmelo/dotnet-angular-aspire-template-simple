using AppTemplate.Core.ValueObjects;
using AppTemplate.Infrastructure.Email;

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
                jwt => jwt.SigningKey.Length >= JwtSettings.MinimumSigningKeyLength,
                $"Jwt:SigningKey must be at least {JwtSettings.MinimumSigningKeyLength} characters."
            )
            .Validate(
                jwt =>
                    jwt.AccessTokenValidity > TimeSpan.Zero
                    && jwt.RefreshTokenValidity > jwt.AccessTokenValidity,
                "Jwt validities must be positive, with the refresh token outliving the access token."
            )
            .Validate(
                jwt => jwt.RefreshCookiePath.StartsWith('/'),
                "Jwt:RefreshCookiePath must start with '/'."
            )
            .ValidateOnStart();

        logger.LogInformation("{Project} were configured", "Options");

        return services;
    }
}
