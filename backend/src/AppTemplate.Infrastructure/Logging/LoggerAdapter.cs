using System.Diagnostics.CodeAnalysis;
using AppTemplate.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AppTemplate.Infrastructure.Logging;

/// <summary>
/// <see cref="ILoggerAdapter{T}"/> over <c>Microsoft.Extensions.Logging</c>, so the logs reach every
/// provider (Serilog's console, OpenTelemetry's exporter) with their structured properties.
/// Registered as an open generic in <c>AddInfrastructureServices</c>.
/// </summary>
[SuppressMessage(
    "Usage",
    "CA2254:Template should be a static expression",
    Justification = "The adapter forwards its callers' templates, which are constants at the call sites."
)]
public sealed class LoggerAdapter<T>(ILoggerFactory loggerFactory) : ILoggerAdapter<T>
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<T>();

    public void LogDebug(string message, params object?[] args)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(message, args);
        }
    }

    public void LogInformation(string message, params object?[] args)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(message, args);
        }
    }

    public void LogWarning(string message, params object?[] args)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(message, args);
        }
    }

    public void LogError(Exception? exception, string message, params object?[] args)
    {
        if (_logger.IsEnabled(LogLevel.Error))
        {
            _logger.LogError(exception, message, args);
        }
    }

    public IDisposable? BeginScope(IReadOnlyDictionary<string, object?> state) =>
        _logger.BeginScope(state);
}
