using System.Diagnostics;
using Ardalis.GuardClauses;

namespace AppTemplate.Web.Behaviors;

/// <summary>
/// Logs every command and query through the Mediator pipeline, by name only: requests carry
/// passwords, codes and personal data, and responses carry personal data, none of which belongs in
/// logs. Never log the request, the response or their properties here (<c>{@Request}</c>,
/// reflection over their properties): nothing redacts them.
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
            logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);
        }

        var sw = Stopwatch.StartNew();

        var response = await next(request, cancellationToken);

        sw.Stop();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Handled {RequestName} in {ElapsedMilliseconds} ms",
                typeof(TRequest).Name,
                sw.ElapsedMilliseconds
            );
        }

        return response;
    }
}
