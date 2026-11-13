using Microsoft.Extensions.Logging;

namespace AppTemplate.Infrastructure;

/// <summary>Infrastructure's startup log lines, source-generated (<c>[LoggerMessage]</c>, CA1848).</summary>
internal static partial class InfrastructureLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "{Project} were configured")]
    public static partial void Configured(ILogger logger, string project);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Project} services registered")]
    public static partial void ServicesRegistered(ILogger logger, string project);
}
