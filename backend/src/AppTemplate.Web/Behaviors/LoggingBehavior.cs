using System.Diagnostics;
using Ardalis.GuardClauses;

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
        Guard.Against.Null(request);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Handling {RequestName} with {@Request}",
                typeof(TRequest).Name,
                request
            );
        }

        var sw = Stopwatch.StartNew();

        var response = await next(request, cancellationToken);

        sw.Stop();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Handled {RequestName} with {Response} in {ElapsedMilliseconds} ms",
                typeof(TRequest).Name,
                response,
                sw.ElapsedMilliseconds
            );
        }

        return response;
    }
}
