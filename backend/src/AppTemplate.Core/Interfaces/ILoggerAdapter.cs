namespace AppTemplate.Core.Interfaces;

/// <summary>
/// The application's logger (the adapter pattern, as in NimblePros/eShopOnWeb): every layer logs
/// through it instead of <c>Microsoft.Extensions.Logging</c>'s <c>ILogger</c>, which only
/// Infrastructure's <c>LoggerAdapter</c> knows. <typeparamref name="T"/> is the log category, the
/// class that logs; resolve it from DI, as with <c>ILogger&lt;T&gt;</c>. Each <c>message</c> is a
/// message template with named placeholders (<c>"User {UserId} signed in"</c>), never an
/// interpolated string, so each value stays a structured property in the logs. Never pass
/// requests, DTOs, passwords, codes or email addresses: nothing redacts them. It has no log scopes:
/// code in Web or Infrastructure that needs one uses <c>ILogger&lt;T&gt;</c>.
/// </summary>
public interface ILoggerAdapter<T>
{
    void LogDebug(string message, params object?[] args);

    void LogInformation(string message, params object?[] args);

    void LogWarning(string message, params object?[] args);

    void LogError(Exception? exception, string message, params object?[] args);
}
