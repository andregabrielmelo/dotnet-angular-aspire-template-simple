using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AppTemplate.UseCases.Behaviors;

/// <summary>
/// Logs every command and query through the Mediator pipeline. The request is destructured
/// ({@Request}), so classified properties ([PersonalData], [SecretData]) are redacted.
/// Configure by adding the service with a scoped lifetime
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger = logger;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken
    )
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Handling {RequestName} with {@Request}",
                typeof(TRequest).Name,
                request
            );
        }

        var sw = Stopwatch.StartNew();

        var response = await next(request, cancellationToken);

        sw.Stop();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            // Not the response itself: a DTO's ToString() prints every property, and nothing
            // classified inside it would be redacted.
            _logger.LogInformation(
                "Handled {RequestName} in {ElapsedMilliseconds} ms",
                typeof(TRequest).Name,
                sw.ElapsedMilliseconds
            );
        }

        return response;
    }
}
