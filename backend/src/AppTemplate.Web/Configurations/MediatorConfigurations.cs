using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Infrastructure;
using AppTemplate.UseCases.Behaviors;
using AppTemplate.UseCases.Users;

namespace AppTemplate.Web.Configurations;

public static class MediatorConfigurations
{
    public static IServiceCollection AddMediatorSourceGenerator(
        this IServiceCollection services,
        Microsoft.Extensions.Logging.ILogger logger
    )
    {
        logger.LogInformation("Registering Mediator SourceGen and Behaviors");
        services.AddMediator(options =>
        {
            // Lifetime: Singleton is fastest per docs; Scoped/Transient also supported.
            options.ServiceLifetime = ServiceLifetime.Scoped;

            // Supply any TYPE from each assembly you want scanned (the generator finds the assembly from the type)
            options.Assemblies =
            [
                typeof(User), // Core
                typeof(UserDto), // UseCases
                typeof(InfrastructureServiceExtensions), // Infrastructure
                typeof(MediatorConfigurations), // Web
            ];

            // Register pipeline behaviors here (order matters).
            options.PipelineBehaviors = [typeof(LoggingBehavior<,>)];

            // If you have stream behaviors:
            // options.StreamPipelineBehaviors = [ typeof(YourStreamBehavior<,>) ];
        });

        return services;
    }
}
