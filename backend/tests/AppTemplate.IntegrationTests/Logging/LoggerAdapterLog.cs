using AppTemplate.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace AppTemplate.IntegrationTests.Logging;

/// <summary>
/// <see cref="LoggerAdapter{T}"/> forwards to Microsoft.Extensions.Logging with the level, the
/// template's structured values, the category and the scope intact. No database involved.
/// </summary>
public class LoggerAdapterLog
{
    private readonly FakeLogCollector _logs = new();
    private readonly LoggerAdapter<LoggerAdapterLog> _logger;

    public LoggerAdapterLog()
    {
        using var factory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(_logs))
        );
        _logger = new LoggerAdapter<LoggerAdapterLog>(factory);
    }

    [Fact]
    public void KeepsTheLevelAndTheStructuredValues()
    {
        _logger.LogWarning("User {UserId} did {Action}", 42, "something");

        var record = _logs.LatestRecord;
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldBe("User 42 did something");
        record.Category.ShouldBe(typeof(LoggerAdapterLog).FullName);
        record.StructuredState.ShouldNotBeNull();
        record.StructuredState.ShouldContain(new KeyValuePair<string, string?>("UserId", "42"));
        record.StructuredState.ShouldContain(
            new KeyValuePair<string, string?>("{OriginalFormat}", "User {UserId} did {Action}")
        );
    }

    [Fact]
    public void LogsTheException()
    {
        var exception = new InvalidOperationException("boom");

        _logger.LogError(exception, "Failed {Step}", "seeding");

        _logs.LatestRecord.Level.ShouldBe(LogLevel.Error);
        _logs.LatestRecord.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public void AddsTheScopeToEveryLogInside()
    {
        using (_logger.BeginScope(new Dictionary<string, object?> { ["UserId"] = 7 }))
        {
            _logger.LogInformation("Inside");
        }
        _logger.LogInformation("Outside");

        var records = _logs.GetSnapshot();
        records[0]
            .Scopes.OfType<IEnumerable<KeyValuePair<string, object?>>>()
            .SelectMany(scope => scope)
            .ShouldContain(new KeyValuePair<string, object?>("UserId", 7));
        records[1].Scopes.ShouldBeEmpty();
    }

    [Fact]
    public void SkipsLevelsThatAreOff()
    {
        var logs = new FakeLogCollector();
        using var factory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Information).AddProvider(new FakeLoggerProvider(logs))
        );
        var logger = new LoggerAdapter<LoggerAdapterLog>(factory);

        logger.LogDebug("Hidden {Value}", 1);

        logs.Count.ShouldBe(0);
    }
}
