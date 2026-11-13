using AppTemplate.Core.Interfaces;
using AppTemplate.Infrastructure;
using AppTemplate.Infrastructure.Email;

namespace AppTemplate.Web.Configurations;

public static class ServiceConfigurations
{
    public static IServiceCollection AddServiceConfigurations(
        this IServiceCollection services,
        IAppLogger logger,
        WebApplicationBuilder builder
    )
    {
        builder.AddInfrastructureServices(builder.Configuration, logger);
        services.AddMediatorSourceGenerator(logger);

        services.AddScoped<IEmailSender, MimeKitEmailSender>();

        logger.LogInformation(
            "{Project} services registered",
            "Mediator Source Generator and Email Sender"
        );

        return services;
    }
}
