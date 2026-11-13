namespace AppTemplate.Core.Interfaces;

/// <summary>
/// The application's logger (the adapter pattern, as in NimblePros/eShopOnWeb): every layer logs
/// through it instead of <c>Microsoft.Extensions.Logging</c>'s <c>ILogger</c>, which only
/// Infrastructure's <c>LoggerAdapter</c> knows. Each <c>message</c> is a message template
/// with named placeholders (<c>"User {UserId} signed in"</c>), never an interpolated string, so
/// each value stays a structured property in the logs. Never pass requests, DTOs, passwords,
/// codes or email addresses: nothing redacts them.
/// </summary>
public interface IAppLogger
{
    void LogDebug(string message, params object?[] args);

    void LogInformation(string message, params object?[] args);

    void LogWarning(string message, params object?[] args);

    void LogError(Exception? exception, string message, params object?[] args);

    /// <summary>
    /// Adds <paramref name="state"/> as properties to every log written until the result is
    /// disposed.
    /// </summary>
    IDisposable? BeginScope(IReadOnlyDictionary<string, object?> state);
}

/// <summary>
/// An <see cref="IAppLogger"/> whose category is <typeparamref name="T"/>, the class that logs;
/// resolve this one from DI, as with <c>ILogger&lt;T&gt;</c>.
/// </summary>
public interface IAppLogger<out T> : IAppLogger;
