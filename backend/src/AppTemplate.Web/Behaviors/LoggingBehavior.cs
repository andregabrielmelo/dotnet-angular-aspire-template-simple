using System.Diagnostics;

namespace AppTemplate.Web.Behaviors;

/// <summary>
/// Logs every command and query through the Mediator pipeline, by name only: requests carry
/// passwords, refresh tokens and personal data, which never belong in logs. Never log the request
/// or its properties here (<c>{@Request}</c>, reflection over its properties) - nothing redacts them.
/// </summary>
public class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);
        var stopwatch = Stopwatch.StartNew();

        var response = await next(request, cancellationToken);

        logger.LogInformation(
            "Handled {RequestName} in {ElapsedMilliseconds} ms",
            typeof(TRequest).Name,
            stopwatch.ElapsedMilliseconds
        );

        return response;
    }
}
