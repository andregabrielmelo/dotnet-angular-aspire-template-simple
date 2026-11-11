using AppTemplate.Infrastructure.Email;
using AppTemplate.Infrastructure.Identity;

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
                    && System.Net.Mail.MailAddress.TryCreate(mail.FromAddress, out _),
                "Mailserver needs a Hostname, a Port between 1 and 65535, and a FromAddress."
            )
            .ValidateOnStart();

        services
            .AddOptions<AccountLinkSettings>()
            .Bind(configuration.GetSection(AccountLinkSettings.SectionName))
            .Validate(
                links =>
                    Uri.TryCreate(links.ClientUrl, UriKind.Absolute, out var url)
                    && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp),
                "AccountLinks:ClientUrl must be the web app's absolute http(s) URL."
            )
            .Validate(
                links =>
                    links.ConfirmEmailPath.StartsWith('/')
                    && links.ResetPasswordPath.StartsWith('/'),
                "AccountLinks paths must start with '/'."
            )
            .ValidateOnStart();

        logger.LogInformation("{Project} were configured", "Options");

        return services;
    }
}
