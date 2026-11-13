using System.Net;
using AppTemplate.Infrastructure.Caching;
using AppTemplate.Infrastructure.Email;
using AppTemplate.Infrastructure.Identity;
using AppTemplate.UseCases.Caching;
using AppTemplate.Web.Settings;

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

        services
            .AddOptions<RateLimitSettings>()
            .Bind(configuration.GetSection(RateLimitSettings.SectionName))
            .Validate(
                limits =>
                    limits.Window > TimeSpan.Zero
                    && limits.AuthenticatedPermitLimit > 0
                    && limits.AnonymousPermitLimit > 0
                    && limits.AuthenticationPermitLimit > 0,
                "RateLimiting needs a positive Window and permit limits."
            )
            .ValidateOnStart();

        services
            .AddOptions<ForwardedHeadersSettings>()
            .Bind(configuration.GetSection(ForwardedHeadersSettings.SectionName))
            .Validate(
                proxies => proxies.KnownProxies.All(proxy => IPAddress.TryParse(proxy, out _)),
                "ForwardedHeaders:KnownProxies must be IP addresses."
            )
            .ValidateOnStart();

        services
            .AddOptions<CachingSettings>()
            .Bind(configuration.GetSection(CachingSettings.SectionName))
            .Validate(
                caching =>
                    caching.Expiration > TimeSpan.Zero
                    && caching.LocalExpiration > TimeSpan.Zero
                    && caching.LocalExpiration <= caching.Expiration,
                "Caching needs a positive Expiration and a LocalExpiration no longer than it."
            )
            .Validate(
                caching =>
                    CacheProfile
                        .List.Where(profile => profile != CacheProfile.None)
                        .All(profile =>
                            caching.Profiles.TryGetValue(profile.Name, out var lifetime)
                            && lifetime.IsValid
                        ),
                $"Caching:Profiles needs every cache profile ({string.Join(", ", CacheProfile.List.Where(profile => profile != CacheProfile.None).Select(profile => profile.Name))}) with a positive Expiration and a LocalExpiration no longer than it."
            )
            .ValidateOnStart();

        StartupLog.Configured(logger, "Options");

        return services;
    }
}
