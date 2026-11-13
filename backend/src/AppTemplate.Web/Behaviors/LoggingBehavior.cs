using System.Diagnostics;
using Ardalis.GuardClauses;

namespace AppTemplate.Web.Behaviors;

/// <summary>
/// Logs every command and query through the Mediator pipeline, by name only: requests carry
/// passwords, codes and personal data, and responses carry personal data, none of which belongs in
/// logs. Never log the request, the response or their properties here (<c>{@Request}</c>,
/// reflection over their properties): nothing redacts them.
/// </summary>
public partial class LoggingBehavior<TRequest, TResponse>(
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

        LogHandling(logger, typeof(TRequest).Name);

        var sw = Stopwatch.StartNew();

        var response = await next(request, cancellationToken);

        sw.Stop();

        LogHandled(logger, typeof(TRequest).Name, sw.ElapsedMilliseconds);

        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {RequestName}")]
    private static partial void LogHandling(ILogger logger, string requestName);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Handled {RequestName} in {ElapsedMilliseconds} ms"
    )]
    private static partial void LogHandled(
        ILogger logger,
        string requestName,
        long elapsedMilliseconds
    );
}
