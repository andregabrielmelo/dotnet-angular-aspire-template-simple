using AppTemplate.Infrastructure.Email;

namespace AppTemplate.Web.Configurations;

public static class OptionConfigurations
{
    public static IServiceCollection AddOptionConfigurations(
        this IServiceCollection services,
        IConfiguration configuration,
        Microsoft.Extensions.Logging.ILogger logger,
        WebApplicationBuilder builder
    )
    {
        services
            .AddOptions<MailserverConfiguration>()
            .Bind(configuration.GetSection(MailserverConfiguration.SectionName))
            .Validate(
                mail => !string.IsNullOrWhiteSpace(mail.Hostname) && mail.Port is > 0 and <= 65535,
                "Mailserver needs a Hostname and a Port between 1 and 65535."
            )
            .ValidateOnStart();

        services
        // Configure Web Behavior
        .Configure<CookiePolicyOptions>(options =>
        {
            options.CheckConsentNeeded = context => true;
            options.MinimumSameSitePolicy = SameSiteMode.None;
        });

        logger.LogInformation("{Project} were configured", "Options");

        return services;
    }
}
